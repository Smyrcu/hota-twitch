#include "post_policy.hpp"

namespace hota_twitch
{

PostPolicy::PostPolicy(std::int64_t minIntervalMs, std::int64_t repostIntervalMs)
    : m_minIntervalMs(minIntervalMs), m_repostIntervalMs(repostIntervalMs)
{
}

void PostPolicy::stateReceived()
{
    m_hasState = true;
}

bool PostPolicy::allows(bool changed, std::int64_t nowMs) const
{
    if (!m_hasState)
    {
        return false;
    }
    if (!m_everPosted)
    {
        return true;
    }
    const std::int64_t since = nowMs - m_lastPostMs;
    if (since < m_minIntervalMs)
    {
        return false;
    }
    return changed || since >= m_repostIntervalMs;
}

void PostPolicy::posted(std::int64_t nowMs)
{
    m_lastPostMs = nowMs;
    m_everPosted = true;
}

std::int64_t PostPolicy::waitForMs(bool changePending, std::int64_t nowMs) const
{
    if (!m_hasState)
    {
        // Nothing to send: the wait ends when the game thread publishes, not when it expires.
        return m_repostIntervalMs;
    }
    if (!m_everPosted)
    {
        return 0;
    }
    const std::int64_t target = changePending ? m_minIntervalMs : m_repostIntervalMs;
    const std::int64_t since = nowMs - m_lastPostMs;
    return since >= target ? 0 : target - since;
}

} // namespace hota_twitch
