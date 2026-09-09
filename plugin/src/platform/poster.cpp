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

std::int64_t nowInMilliseconds()
{
    return duration_cast<milliseconds>(steady_clock::now().time_since_epoch()).count();
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

void Poster::run()
{
    if (!m_http.open(m_config.backendUrl, m_config.token))
    {
        m_log.error("no connection to " + m_config.backendUrl + "; nothing will be posted");
        return;
    }
    m_log.info("posting state to " + m_config.backendUrl);

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

        const bool changed = !m_hasDelivered || !current.sameStateAs(m_delivered);
        if (!m_policy.allows(changed, nowInMilliseconds()))
        {
            continue;
        }

        serializeState(current, m_config.codepage, document);
        m_hasDelivered = deliver(document);
        if (m_hasDelivered)
        {
            m_delivered = current;
        }
        m_policy.posted(nowInMilliseconds());
    }
    m_log.info("the worker stopped");
}

bool Poster::deliver(const std::string& document)
{
    const int status = m_http.postState(document);
    switch (status)
    {
    case 202:
        return true;
    case 0:
        // Already logged by the client; nothing is queued, the next tick tries again.
        break;
    case 401:
        m_log.error("the backend does not know this token - generate a new one on the "
                    "extension configuration page and put it in hota-twitch.ini");
        break;
    case 413:
        m_log.warn("the state document was too large for the backend and was dropped");
        break;
    case 429:
        m_log.debug("the backend asked for fewer posts");
        break;
    default:
        m_log.warn("the backend answered " + std::to_string(status));
        break;
    }
    return false;
}

} // namespace hota_twitch::platform
