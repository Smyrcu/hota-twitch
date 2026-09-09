#include "plugin.hpp"

#include <windows.h>

namespace
{

HMODULE g_module = nullptr;

/// Setting the plugin up means reaching the HD Mod patcher, and reaching it means
/// `LoadLibrary`, which must never be called while the loader lock is held. So `DllMain` only
/// starts this thread and returns; the work happens once the loader has let go.
DWORD WINAPI bootstrap(LPVOID)
{
    hota_twitch::Plugin::instance().initialise(g_module);
    return 0;
}

} // namespace

/// Entry point for a loader that links the DLL and calls into it. Whichever of this and the
/// process-attach path runs first wins; the other one finds the work already done.
extern "C" __declspec(dllexport) void __cdecl HotaTwitch_Init(void)
{
    hota_twitch::Plugin::instance().initialise(g_module);
}

extern "C" BOOL WINAPI DllMain(HINSTANCE instance, DWORD reason, LPVOID)
{
    if (reason != DLL_PROCESS_ATTACH)
    {
        return TRUE;
    }
    g_module = static_cast<HMODULE>(instance);
    DisableThreadLibraryCalls(instance);
    const HANDLE thread = CreateThread(nullptr, 0, &bootstrap, nullptr, 0, nullptr);
    if (thread != nullptr)
    {
        CloseHandle(thread);
    }
    return TRUE;
}
