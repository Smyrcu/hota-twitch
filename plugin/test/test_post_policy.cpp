#include "framework.hpp"

#include "core/post_policy.hpp"

using namespace hota_twitch;

namespace
{

constexpr std::int64_t kMinInterval = 500;
constexpr std::int64_t kRepostInterval = 10000;

PostPolicy fresh()
{
    return PostPolicy(kMinInterval, kRepostInterval);
}

} // namespace

HOTA_TEST(the_very_first_document_always_goes_out)
{
    const PostPolicy policy = fresh();

    HOTA_CHECK(policy.allows(false, 0));
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
