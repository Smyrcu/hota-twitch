#pragma once

#include "layout.hpp"

#include <cstring>
#include <type_traits>

/// Reading fields the plugin locates by offset rather than through an NH3API type. Pointers
/// that come from NH3API globals or from the game's own accessors are the game's business and
/// are used directly; these helpers exist for the HotA-specific offsets, where a wrong guess
/// would otherwise dereference a small integer.
namespace hota_twitch::game
{

inline bool looksLikePointer(const void* value)
{
    return reinterpret_cast<std::uintptr_t>(value) >= layout::kLowestValidPointer;
}

/// Whether `size` bytes from `address` are committed and readable. A stray read inside the
/// game is an access violation, not something a catch block can undo, so the one pointer the
/// plugin invents rather than receives - the HotA town extension record - is checked here
/// before it is followed.
bool isReadable(const void* address, std::size_t size);

/// Reads a trivially copyable value `offset` bytes into the object at `base`. The caller has
/// already established that `base` is a live game object.
template<typename T>
T readAt(const void* base, std::size_t offset)
{
    static_assert(std::is_trivially_copyable_v<T>, "raw game reads must be trivially copyable");
    T value{};
    std::memcpy(&value, static_cast<const unsigned char*>(base) + offset, sizeof(T));
    return value;
}

/// Reads an absolute game address.
template<typename T>
T readAbsolute(std::uintptr_t address)
{
    static_assert(std::is_trivially_copyable_v<T>, "raw game reads must be trivially copyable");
    T value{};
    std::memcpy(&value, reinterpret_cast<const void*>(address), sizeof(T));
    return value;
}

} // namespace hota_twitch::game
