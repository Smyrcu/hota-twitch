#pragma once

#include <string>

#include <windows.h>

namespace hota_twitch::platform
{

/// The directory the given module was loaded from, with a trailing separator. The plugin keeps
/// its ini and its log next to the DLL, not next to the game executable, so that nothing the
/// plugin writes lands in the game directory unless the streamer put the DLL there.
std::string moduleDirectory(HMODULE module);

} // namespace hota_twitch::platform
