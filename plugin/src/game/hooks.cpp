#include "hooks.hpp"

#include "game/layout.hpp"

#include <nh3api/core.hpp>

#include <windows.h>

namespace hota_twitch::game
{
namespace
{

/// `docs/protocol.md` allows two posts a second; taking a snapshot more often than that would
/// only cost the game thread time for a document nobody sends.
constexpr unsigned long kMinimumIntervalMs = 300;

/// The name the plugin registers with the HD Mod patcher. It shows up in the patcher's own
/// listings, so it says who the hooks belong to.
constexpr const char* kPatcherOwner = "HD.Plugin.hota-twitch";

/// The hooks are C function pointers, so the instance they belong to has to be reachable
/// without an argument. Exactly one Hooks object exists for the life of the process.
Hooks* g_hooks = nullptr;

void __stdcall onAdventureUpdate(HiHook* hook, void* self, int drawWindow, int update)
{
    THISCALL_3(void, hook->GetOriginalFunc(), self, drawWindow, update);
    if (g_hooks != nullptr)
    {
        g_hooks->snapshotIfDue();
    }
}

std::int32_t __stdcall onAddManager(HiHook* hook, void* self, void* manager, std::int32_t priority)
{
    const std::int32_t result =
        THISCALL_3(std::int32_t, hook->GetOriginalFunc(), self, manager, priority);
    if (g_hooks != nullptr)
    {
        g_hooks->snapshot();
    }
    return result;
}

void __stdcall onRemoveManager(HiHook* hook, void* self, void* manager)
{
    THISCALL_2(void, hook->GetOriginalFunc(), self, manager);
    if (g_hooks != nullptr)
    {
        g_hooks->snapshot();
    }
}

} // namespace

Hooks::Hooks(Logger& log, Reader& reader, SnapshotSink& sink)
    : m_log(log), m_reader(reader), m_sink(sink)
{
}

Hooks::~Hooks()
{
    if (g_hooks == this)
    {
        g_hooks = nullptr;
    }
}

bool Hooks::install()
{
    if (m_installed)
    {
        return true;
    }
    Patcher* const patcher = GetPatcher();
    if (patcher == nullptr)
    {
        return false;
    }
    PatcherInstance* const instance = patcher->CreateInstance(kPatcherOwner);
    if (instance == nullptr)
    {
        m_log.error("the HD Mod patcher refused to register the plugin; no state is posted");
        return false;
    }

    g_hooks = this;
    instance->WriteHiHook(layout::kAdvManagerUpdateScreen, SPLICE_, EXTENDED_, THISCALL_,
                          &onAdventureUpdate);
    instance->WriteHiHook(layout::kExecutiveAddManager, SPLICE_, EXTENDED_, THISCALL_, &onAddManager);
    instance->WriteHiHook(layout::kExecutiveRemoveManager, SPLICE_, EXTENDED_, THISCALL_,
                          &onRemoveManager);
    m_installed = true;
    m_log.info("hooks installed on the adventure screen update and the manager list");
    return true;
}

void Hooks::snapshot()
{
    if (m_takingSnapshot)
    {
        return;
    }
    m_takingSnapshot = true;
    m_lastSnapshotTicks = GetTickCount();
    // The game has no idea what a C++ exception is: whatever goes wrong while reading, the
    // game thread has to come back out of here and carry on.
    try
    {
        m_reader.read(m_building);
        m_sink.publish(m_building);
    }
    catch (const std::exception& error)
    {
        m_log.error(std::string("taking a snapshot failed: ") + error.what());
    }
    catch (...)
    {
        m_log.error("taking a snapshot failed");
    }
    m_takingSnapshot = false;
}

void Hooks::snapshotIfDue()
{
    // GetTickCount wraps every 49 days; unsigned subtraction keeps the difference right
    // across the wrap.
    if (GetTickCount() - m_lastSnapshotTicks < kMinimumIntervalMs)
    {
        return;
    }
    snapshot();
}

} // namespace hota_twitch::game
