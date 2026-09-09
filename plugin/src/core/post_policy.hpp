#pragma once

#include <cstdint>

namespace hota_twitch
{

/// When a state document may go out, following `docs/protocol.md` section 2: post on change
/// and at least every ten seconds while a game is loaded, and never more than twice a second.
class PostPolicy
{
public:
    PostPolicy(std::int64_t minIntervalMs, std::int64_t repostIntervalMs);

    bool allows(bool changed, std::int64_t nowMs) const;

    void posted(std::int64_t nowMs);

private:
    std::int64_t m_minIntervalMs;
    std::int64_t m_repostIntervalMs;
    std::int64_t m_lastPostMs = 0;
    bool m_everPosted = false;
};

} // namespace hota_twitch
