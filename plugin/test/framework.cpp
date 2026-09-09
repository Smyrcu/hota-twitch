#include "framework.hpp"

#include <cstdio>
#include <stdexcept>

namespace hota_twitch::test
{
namespace
{

struct Failure : std::runtime_error
{
    using std::runtime_error::runtime_error;
};

} // namespace

std::vector<Case>& registry()
{
    static std::vector<Case> cases;
    return cases;
}

void fail(const char* file, int line, const std::string& message)
{
    throw Failure(std::string(file) + ":" + std::to_string(line) + ": " + message);
}

Registrar::Registrar(const char* name, TestBody body)
{
    registry().push_back({name, body});
}

} // namespace hota_twitch::test

int main()
{
    int failed = 0;
    for (const auto& [name, run] : hota_twitch::test::registry())
    {
        try
        {
            run();
        }
        catch (const std::exception& error)
        {
            ++failed;
            std::printf("FAIL %s\n     %s\n", name, error.what());
        }
    }
    const std::size_t total = hota_twitch::test::registry().size();
    std::printf("%zu tests, %d failed\n", total, failed);
    return failed == 0 ? 0 : 1;
}
