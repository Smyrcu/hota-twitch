#include "post_policy.hpp"

namespace hota_twitch
{

PostPolicy::PostPolicy(std::int64_t minIntervalMs, std::int64_t repostIntervalMs)
    : m_minIntervalMs(minIntervalMs), m_repostIntervalMs(repostIntervalMs)
{
}

bool PostPolicy::allows(bool changed, std::int64_t nowMs) const
{
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

} // namespace hota_twitch
