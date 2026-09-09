#include "memory.hpp"

#include <windows.h>

namespace hota_twitch::game
{

bool isReadable(const void* address, std::size_t size)
{
    if (!looksLikePointer(address) || size == 0)
    {
        return false;
    }
    const auto* cursor = static_cast<const unsigned char*>(address);
    const unsigned char* const last = cursor + size - 1;
    while (cursor <= last)
    {
        MEMORY_BASIC_INFORMATION region{};
        if (VirtualQuery(cursor, &region, sizeof(region)) != sizeof(region))
        {
            return false;
        }
        if (region.State != MEM_COMMIT)
        {
            return false;
        }
        constexpr DWORD kReadable = PAGE_READONLY | PAGE_READWRITE | PAGE_WRITECOPY |
                                    PAGE_EXECUTE_READ | PAGE_EXECUTE_READWRITE |
                                    PAGE_EXECUTE_WRITECOPY;
        if ((region.Protect & kReadable) == 0 || (region.Protect & PAGE_GUARD) != 0)
        {
            return false;
        }
        const auto* const regionEnd =
            static_cast<const unsigned char*>(region.BaseAddress) + region.RegionSize;
        if (regionEnd <= cursor)
        {
            return false;
        }
        cursor = regionEnd;
    }
    return true;
}

} // namespace hota_twitch::game
