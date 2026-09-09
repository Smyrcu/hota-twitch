#pragma once

#include "snapshot.hpp"

#include <chrono>
#include <condition_variable>
#include <mutex>

namespace hota_twitch
{

enum class TakeResult
{
    Received,
    TimedOut,
    Stopped,
};

/// One-slot handover between the game thread and the worker. A snapshot published while the
/// previous one is still waiting replaces it, so the worker always sees the newest state and
/// the game thread never blocks on the network. Publishing swaps rather than copies, which
/// hands the producer back the buffers the consumer is done with.
class SnapshotMailbox
{
public:
    void publish(StateSnapshot& fresh);

    TakeResult take(StateSnapshot& into, std::chrono::milliseconds timeout);

    void stop();

private:
    std::mutex m_mutex;
    std::condition_variable m_ready;
    StateSnapshot m_slot;
    bool m_full = false;
    bool m_stopped = false;
};

} // namespace hota_twitch
