#include "mailbox.hpp"

#include <utility>

namespace hota_twitch
{

void SnapshotMailbox::publish(StateSnapshot& fresh)
{
    {
        const std::lock_guard<std::mutex> lock(m_mutex);
        if (m_stopped)
        {
            return;
        }
        std::swap(m_slot, fresh);
        m_full = true;
    }
    m_ready.notify_one();
}

TakeResult SnapshotMailbox::take(StateSnapshot& into, std::chrono::milliseconds timeout)
{
    std::unique_lock<std::mutex> lock(m_mutex);
    if (!m_ready.wait_for(lock, timeout, [this] { return m_full || m_stopped; }))
    {
        return TakeResult::TimedOut;
    }
    if (m_stopped)
    {
        return TakeResult::Stopped;
    }
    std::swap(m_slot, into);
    m_full = false;
    return TakeResult::Received;
}

void SnapshotMailbox::stop()
{
    {
        const std::lock_guard<std::mutex> lock(m_mutex);
        m_stopped = true;
    }
    m_ready.notify_all();
}

} // namespace hota_twitch
