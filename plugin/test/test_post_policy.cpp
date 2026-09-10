#include "framework.hpp"

#include "core/post_policy.hpp"

#include <cstddef>
#include <vector>

using namespace hota_twitch;

namespace
{

constexpr std::int64_t kMinInterval = 500;
constexpr std::int64_t kRepostInterval = 10000;

/// A policy that has been handed a snapshot, which is the state the worker spends its life in.
PostPolicy fresh()
{
    PostPolicy policy(kMinInterval, kRepostInterval);
    policy.stateReceived();
    return policy;
}

/// Runs the policy the way the worker runs it: sleep for as long as it asks, then post if it
/// allows one. `changePending` says whether the state the worker holds is one the backend has
/// not seen. Returns the times the posts went out.
std::vector<std::int64_t> cadence(PostPolicy& policy, bool changePending, std::int64_t untilMs)
{
    std::vector<std::int64_t> posts;
    std::int64_t now = 0;
    while (now <= untilMs)
    {
        now += policy.waitForMs(changePending, now);
        if (now > untilMs || !policy.allows(changePending, now))
        {
            break;
        }
        posts.push_back(now);
        policy.posted(now);
    }
    return posts;
}

} // namespace

HOTA_TEST(the_very_first_document_always_goes_out)
{
    const PostPolicy policy = fresh();

    HOTA_CHECK(policy.allows(false, 0));
}

HOTA_TEST(nothing_goes_out_before_the_game_thread_has_handed_over_a_state)
{
    const PostPolicy policy(kMinInterval, kRepostInterval);

    HOTA_CHECK(!policy.allows(true, 0));
    HOTA_CHECK(!policy.allows(false, kRepostInterval * 10));
}

HOTA_TEST(with_no_state_the_worker_waits_rather_than_spins)
{
    const PostPolicy policy(kMinInterval, kRepostInterval);

    HOTA_CHECK_EQ(policy.waitForMs(true, 0), kRepostInterval);
}

HOTA_TEST(a_changed_state_goes_out_once_the_rate_limit_allows_it)
{
    PostPolicy policy = fresh();
    policy.posted(1000);

    HOTA_CHECK(!policy.allows(true, 1000 + kMinInterval - 1));
    HOTA_CHECK(policy.allows(true, 1000 + kMinInterval));
}

HOTA_TEST(an_unchanged_state_waits_for_the_repost_interval)
{
    PostPolicy policy = fresh();
    policy.posted(1000);

    HOTA_CHECK(!policy.allows(false, 1000 + kMinInterval));
    HOTA_CHECK(!policy.allows(false, 1000 + kRepostInterval - 1));
    HOTA_CHECK(policy.allows(false, 1000 + kRepostInterval));
}

HOTA_TEST(the_rate_limit_outranks_the_repost_interval)
{
    PostPolicy policy = fresh();
    policy.posted(1000);
    policy.posted(1000 + kRepostInterval);

    HOTA_CHECK(!policy.allows(false, 1000 + kRepostInterval + 1));
}

HOTA_TEST(never_more_than_two_posts_a_second)
{
    PostPolicy policy = fresh();
    std::int64_t now = 0;
    int posts = 0;
    for (int tick = 0; tick < 1000; ++tick, now += 10)
    {
        if (policy.allows(true, now))
        {
            ++posts;
            policy.posted(now);
        }
    }

    // Just under ten seconds of ticks, all of them offering a changed state: one post at the
    // start and one every 500 ms after it, which is the two-a-second ceiling and no more.
    HOTA_CHECK_EQ(posts, 20);
}

HOTA_TEST(a_game_that_produces_no_new_state_is_still_reposted_every_ten_seconds)
{
    PostPolicy policy = fresh();

    const std::vector<std::int64_t> posts = cadence(policy, false, 30000);

    const std::vector<std::int64_t> expected{0, 10000, 20000, 30000};
    HOTA_CHECK_EQ(posts.size(), expected.size());
    for (std::size_t index = 0; index < posts.size(); ++index)
    {
        HOTA_CHECK_EQ(posts[index], expected[index]);
    }
}

HOTA_TEST(a_fresh_state_waits_for_the_rate_limit_and_not_for_the_repost_interval)
{
    PostPolicy policy = fresh();
    policy.posted(1000);

    // A snapshot the backend has not seen arrives a tenth of a second after the last post.
    HOTA_CHECK_EQ(policy.waitForMs(true, 1100), 400);
    HOTA_CHECK(!policy.allows(true, 1100));
    HOTA_CHECK(policy.allows(true, 1500));

    // The same moment with nothing new to say waits out the rest of the repost interval.
    HOTA_CHECK_EQ(policy.waitForMs(false, 1100), 9900);
}

HOTA_TEST(a_state_that_keeps_changing_is_posted_twice_a_second_and_no_faster)
{
    PostPolicy policy = fresh();

    const std::vector<std::int64_t> posts = cadence(policy, true, 2000);

    const std::vector<std::int64_t> expected{0, 500, 1000, 1500, 2000};
    HOTA_CHECK_EQ(posts.size(), expected.size());
    for (std::size_t index = 0; index < posts.size(); ++index)
    {
        HOTA_CHECK_EQ(posts[index], expected[index]);
    }
}

HOTA_TEST(the_wait_is_zero_exactly_when_a_post_is_due)
{
    PostPolicy policy = fresh();
    policy.posted(1000);

    HOTA_CHECK_EQ(policy.waitForMs(true, 1000 + kMinInterval), 0);
    HOTA_CHECK_EQ(policy.waitForMs(false, 1000 + kRepostInterval), 0);
    HOTA_CHECK_EQ(policy.waitForMs(false, 1000 + kRepostInterval * 2), 0);
}
