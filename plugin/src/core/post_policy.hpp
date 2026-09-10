#pragma once

#include <cstdint>

namespace hota_twitch
{

/// When a state document may go out, following `docs/protocol.md` section 2: post on change
/// and at least every ten seconds while a game is loaded, and never more than twice a second.
///
/// The whole cadence lives here rather than in the worker, so that it can be exercised against
/// a clock the test drives. The worker keeps time by asking `waitForMs` how long it may block
/// on the next snapshot; that is what makes the ten-second repost independent of the game
/// producing one, which is the difference between a stream that keeps sending while the
/// streamer sits still and one that goes quiet.
class PostPolicy
{
public:
    PostPolicy(std::int64_t minIntervalMs, std::int64_t repostIntervalMs);

    /// Records that the game thread has handed over a snapshot. Until one arrives there is
    /// nothing to send, and the cadence has not begun.
    void stateReceived();

    /// Whether a document may go out at `nowMs`. `changed` says whether it differs from the
    /// one the backend last accepted.
    bool allows(bool changed, std::int64_t nowMs) const;

    void posted(std::int64_t nowMs);

    /// How long the worker may wait for the next snapshot before the clock alone calls for a
    /// post. Zero when one is due already. `changePending` says whether the worker is holding
    /// a state the backend has not seen: that one only has to clear the rate limit, while an
    /// unchanged state waits out the repost interval.
    std::int64_t waitForMs(bool changePending, std::int64_t nowMs) const;

private:
    std::int64_t m_minIntervalMs;
    std::int64_t m_repostIntervalMs;
    std::int64_t m_lastPostMs = 0;
    bool m_hasState = false;
    bool m_everPosted = false;
};

} // namespace hota_twitch
