#pragma once

#include "core/config.hpp"
#include "core/mailbox.hpp"
#include "core/post_policy.hpp"
#include "platform/http_client.hpp"

#include <cstdint>
#include <string>
#include <thread>

namespace hota_twitch::platform
{

/// The worker: takes snapshots out of the mailbox, turns them into state documents and posts
/// them to the backend. It never touches the game and never blocks the game thread - the game
/// side of the mailbox only ever holds a mutex long enough to swap two pointers.
class Poster
{
public:
    Poster(Logger& log, SnapshotMailbox& mailbox, const Config& config);
    ~Poster();

    Poster(const Poster&) = delete;
    Poster& operator=(const Poster&) = delete;

    void start();
    void stop();

private:
    /// The thread entry point: nothing but the exception barrier around the loop.
    void run();
    void deliverLoop();
    /// Opens the connection if it is not up, and says whether posting can go ahead.
    bool connect(std::int64_t nowMs);
    /// Sends the document. Returns whether the backend accepted it.
    bool deliver(const std::string& document);
    void backOff(std::int64_t nowMs, const std::string& reason);

    Logger& m_log;
    SnapshotMailbox& m_mailbox;
    Config m_config;
    HttpClient m_http;
    PostPolicy m_policy;
    std::thread m_thread;
    /// The state as the backend last accepted it, so an unchanged game does not generate
    /// traffic. Dropped when a post fails, so the retry goes out even if nothing moved.
    StateSnapshot m_delivered;
    bool m_hasDelivered = false;
    bool m_connected = false;
    bool m_oversizeReported = false;
    std::int64_t m_backoffMs = 0;
    std::int64_t m_retryAfterMs = 0;
};

} // namespace hota_twitch::platform
