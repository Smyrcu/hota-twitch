#include "framework.hpp"

#include "core/mailbox.hpp"

#include <thread>

using namespace hota_twitch;
using namespace std::chrono_literals;

namespace
{

StateSnapshot snapshotWithTimestamp(std::int64_t timestamp)
{
    StateSnapshot state;
    state.timestamp = timestamp;
    state.screen = Screen::Adventure;
    return state;
}

} // namespace

HOTA_TEST(mailbox_hands_a_published_snapshot_to_the_consumer)
{
    SnapshotMailbox mailbox;
    StateSnapshot published = snapshotWithTimestamp(1);
    mailbox.publish(published);

    StateSnapshot taken;
    HOTA_CHECK(mailbox.take(taken, 10ms) == TakeResult::Received);
    HOTA_CHECK_EQ(taken.timestamp, std::int64_t{1});
}

HOTA_TEST(mailbox_keeps_only_the_newest_snapshot)
{
    SnapshotMailbox mailbox;
    StateSnapshot first = snapshotWithTimestamp(1);
    StateSnapshot second = snapshotWithTimestamp(2);
    mailbox.publish(first);
    mailbox.publish(second);

    StateSnapshot taken;
    HOTA_CHECK(mailbox.take(taken, 10ms) == TakeResult::Received);
    HOTA_CHECK_EQ(taken.timestamp, std::int64_t{2});
    HOTA_CHECK(mailbox.take(taken, 10ms) == TakeResult::TimedOut);
}

HOTA_TEST(mailbox_hands_the_spent_snapshot_back_to_the_producer)
{
    SnapshotMailbox mailbox;
    StateSnapshot published = snapshotWithTimestamp(1);
    published.heroes.reserve(8);
    const std::size_t capacity = published.heroes.capacity();
    mailbox.publish(published);

    StateSnapshot taken = snapshotWithTimestamp(7);
    HOTA_CHECK(mailbox.take(taken, 10ms) == TakeResult::Received);
    // The producer's buffer travelled to the consumer, and the consumer's snapshot went back
    // into the slot rather than being thrown away.
    HOTA_CHECK_EQ(taken.heroes.capacity(), capacity);
    HOTA_CHECK_EQ(published.timestamp, std::int64_t{0});
}

HOTA_TEST(mailbox_times_out_when_nothing_is_published)
{
    SnapshotMailbox mailbox;

    StateSnapshot taken;
    HOTA_CHECK(mailbox.take(taken, 5ms) == TakeResult::TimedOut);
}

HOTA_TEST(mailbox_wakes_a_waiting_consumer_when_it_is_stopped)
{
    SnapshotMailbox mailbox;
    std::thread stopper([&mailbox] {
        std::this_thread::sleep_for(5ms);
        mailbox.stop();
    });

    StateSnapshot taken;
    const TakeResult result = mailbox.take(taken, 5s);
    stopper.join();

    HOTA_CHECK(result == TakeResult::Stopped);
}

HOTA_TEST(a_stopped_mailbox_accepts_nothing_further)
{
    SnapshotMailbox mailbox;
    mailbox.stop();

    StateSnapshot published = snapshotWithTimestamp(1);
    mailbox.publish(published);

    StateSnapshot taken;
    HOTA_CHECK(mailbox.take(taken, 10ms) == TakeResult::Stopped);
    HOTA_CHECK_EQ(published.timestamp, std::int64_t{1});
}

HOTA_TEST(mailbox_carries_snapshots_across_threads)
{
    SnapshotMailbox mailbox;
    std::int64_t received = 0;

    std::thread consumer([&mailbox, &received] {
        StateSnapshot taken;
        if (mailbox.take(taken, 5s) == TakeResult::Received)
        {
            received = taken.timestamp;
        }
    });

    std::this_thread::sleep_for(5ms);
    StateSnapshot published = snapshotWithTimestamp(42);
    mailbox.publish(published);
    consumer.join();

    HOTA_CHECK_EQ(received, std::int64_t{42});
}
