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
///
/// The worker keeps the cadence on its own clock. A snapshot arriving is what changes the
/// document, not what drives the sending: once the game thread has handed over one state, the
/// ten-second repost carries on whether or not the game produces another. That matters because
/// the game stops redrawing when nothing happens on screen, and `docs/protocol.md` section 2
/// asks for a post at least every ten seconds for as long as a game is loaded.
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
    /// Whether the worker holds a state the backend has not accepted.
    bool unsent(const StateSnapshot& state) const;
    /// How long the worker may block on the mailbox: whatever the cadence allows, but never
    /// past the moment a backed-off connection may be tried again.
    std::int64_t waitMs(bool changePending, std::int64_t nowMs) const;
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
    bool m_announced = false;
    bool m_oversizeReported = false;
    std::int64_t m_backoffMs = 0;
    std::int64_t m_retryAfterMs = 0;
};

} // namespace hota_twitch::platform
