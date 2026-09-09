#include "poster.hpp"

#include "core/serialize.hpp"

#include <chrono>

namespace hota_twitch::platform
{
namespace
{

using namespace std::chrono;

/// `docs/protocol.md` section 2: at most two posts a second, and one at least every ten
/// seconds while a game is loaded.
constexpr std::int64_t kMinIntervalMs = 500;
constexpr std::int64_t kRepostIntervalMs = 10000;

/// How long the worker waits for a snapshot before looking at the clock again. Short enough
/// that the ten-second repost is not late, long enough that an idle game costs nothing.
constexpr milliseconds kWait{500};

/// After a failed post the worker backs off rather than hammering a backend that is down or
/// rate limiting it, up to a ceiling that still recovers quickly once the outage ends.
constexpr std::int64_t kFirstBackoffMs = 1000;
constexpr std::int64_t kMaxBackoffMs = 30000;

/// `docs/protocol.md` section 2 answers 413 above this. Sending it anyway would only earn a
/// rejection, so an oversized document is dropped here with one line in the log.
constexpr std::size_t kMaxDocumentBytes = 64 * 1024;

std::int64_t steadyMilliseconds()
{
    return duration_cast<milliseconds>(steady_clock::now().time_since_epoch()).count();
}

/// The producer clock `docs/protocol.md` asks for, stamped when the document goes out rather
/// than when the game was read: a repost of an unchanged state is news as of now.
std::int64_t wallClockMilliseconds()
{
    return duration_cast<milliseconds>(system_clock::now().time_since_epoch()).count();
}

} // namespace

Poster::Poster(Logger& log, SnapshotMailbox& mailbox, const Config& config)
    : m_log(log), m_mailbox(mailbox), m_config(config), m_http(log),
      m_policy(kMinIntervalMs, kRepostIntervalMs)
{
}

Poster::~Poster()
{
    stop();
}

void Poster::start()
{
    if (m_thread.joinable())
    {
        return;
    }
    m_thread = std::thread([this] { run(); });
}

void Poster::stop()
{
    if (!m_thread.joinable())
    {
        return;
    }
    m_mailbox.stop();
    m_thread.join();
}

/// An exception escaping a thread function is `std::terminate`, which would take the game down
/// mid-stream. Everything the loop does that can throw is inside this barrier.
void Poster::run()
{
    try
    {
        deliverLoop();
    }
    catch (const std::bad_alloc&)
    {
        m_log.error("out of memory in the worker; no more state is posted");
    }
    catch (...)
    {
        m_log.error("the worker stopped on an unexpected error; no more state is posted");
    }
}

void Poster::deliverLoop()
{
    StateSnapshot current;
    std::string document;
    bool haveState = false;
    for (;;)
    {
        const TakeResult result = m_mailbox.take(current, kWait);
        if (result == TakeResult::Stopped)
        {
            break;
        }
        if (result == TakeResult::Received)
        {
            haveState = true;
        }
        else if (!haveState)
        {
            continue;
        }

        const std::int64_t now = steadyMilliseconds();
        if (now < m_retryAfterMs || !connect(now))
        {
            continue;
        }

        const bool changed = !m_hasDelivered || !current.sameStateAs(m_delivered);
        if (!m_policy.allows(changed, now))
        {
            continue;
        }

        current.timestamp = wallClockMilliseconds();
        serializeState(current, m_config.codepage, document);
        m_policy.posted(steadyMilliseconds());
        m_hasDelivered = deliver(document);
        if (m_hasDelivered)
        {
            m_delivered = current;
        }
    }
    m_log.info("the worker stopped");
}

/// Opening the connection is retried rather than given up on: the streamer may well start the
/// game before the network is up, and INSTALL.md promises the plugin sorts itself out.
bool Poster::connect(std::int64_t nowMs)
{
    if (m_connected)
    {
        return true;
    }
    m_connected = m_http.open(m_config.backendUrl, m_config.token);
    if (m_connected)
    {
        m_log.info("posting state to " + m_config.backendUrl);
        m_backoffMs = 0;
        return true;
    }
    backOff(nowMs, "no connection to " + m_config.backendUrl);
    return false;
}

bool Poster::deliver(const std::string& document)
{
    if (document.size() > kMaxDocumentBytes)
    {
        if (!m_oversizeReported)
        {
            m_oversizeReported = true;
            m_log.warn("the state document is larger than the backend accepts (" +
                       std::to_string(document.size()) + " bytes) and is not being sent");
        }
        return false;
    }
    m_oversizeReported = false;

    const int status = m_http.postState(document);
    if (status == 202)
    {
        m_backoffMs = 0;
        m_retryAfterMs = 0;
        return true;
    }

    const std::int64_t now = steadyMilliseconds();
    switch (status)
    {
    case 0:
        // The client already said what went wrong. The connection may be gone for good, so it
        // is reopened on the next attempt rather than reused.
        m_connected = false;
        backOff(now, "the backend could not be reached");
        break;
    case 401:
        backOff(now, "the backend does not know this token - generate a new one on the "
                     "extension configuration page and put it in hota-twitch.ini");
        break;
    case 429:
        backOff(now, "the backend asked for fewer posts");
        break;
    default:
        backOff(now, "the backend answered " + std::to_string(status));
        break;
    }
    return false;
}

/// Doubles the wait after each consecutive failure and logs only the first one of a streak, so
/// an outage costs the streamer one line rather than two a second for as long as it lasts.
void Poster::backOff(std::int64_t nowMs, const std::string& reason)
{
    if (m_backoffMs == 0)
    {
        m_log.warn(reason);
        m_backoffMs = kFirstBackoffMs;
    }
    else if (m_backoffMs < kMaxBackoffMs)
    {
        m_backoffMs = m_backoffMs * 2 < kMaxBackoffMs ? m_backoffMs * 2 : kMaxBackoffMs;
    }
    m_retryAfterMs = nowMs + m_backoffMs;
}

} // namespace hota_twitch::platform
