#include "module_path.hpp"

#include <vector>

namespace hota_twitch::platform
{

std::string moduleDirectory(HMODULE module)
{
    std::vector<char> path(MAX_PATH);
    for (;;)
    {
        const DWORD written =
            GetModuleFileNameA(module, path.data(), static_cast<DWORD>(path.size()));
        if (written == 0)
        {
            return {};
        }
        if (written < path.size())
        {
            path.resize(written);
            break;
        }
        path.resize(path.size() * 2);
    }

    const std::string full(path.begin(), path.end());
    const std::size_t separator = full.find_last_of("\\/");
    return separator == std::string::npos ? std::string{} : full.substr(0, separator + 1);
}

} // namespace hota_twitch::platform
