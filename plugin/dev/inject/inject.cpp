// Development loader. Waits for the game to appear and makes it load the plugin from wherever
// the DLL was built, so that a change can be tried without putting anything in the game folder
// and without waiting for the loading mechanism to be settled. Works under Wine and Proton.
//
//   inject.exe <absolute path to hota-twitch.dll> [--process "h3hota HD.exe"] [--wait 60]
//
// This is not how a streamer installs the plugin; see INSTALL.md for that.

#include <windows.h>

#include <tlhelp32.h>

#include <cstdio>
#include <cstring>
#include <string>

namespace
{

constexpr const char* kDefaultProcess = "h3hota HD.exe";
constexpr DWORD kPollIntervalMs = 500;

DWORD findProcess(const char* name)
{
    const HANDLE snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
    if (snapshot == INVALID_HANDLE_VALUE)
    {
        return 0;
    }
    PROCESSENTRY32 entry{};
    entry.dwSize = sizeof(entry);
    DWORD pid = 0;
    if (Process32First(snapshot, &entry) != FALSE)
    {
        do
        {
            if (_stricmp(entry.szExeFile, name) == 0)
            {
                pid = entry.th32ProcessID;
                break;
            }
        } while (Process32Next(snapshot, &entry) != FALSE);
    }
    CloseHandle(snapshot);
    return pid;
}

/// Writes the path into the target and starts a thread on LoadLibraryA. The thread's exit code
/// is the module handle, so zero means the game refused the DLL.
bool loadInto(DWORD pid, const std::string& dllPath)
{
    const HANDLE process =
        OpenProcess(PROCESS_CREATE_THREAD | PROCESS_VM_OPERATION | PROCESS_VM_WRITE |
                        PROCESS_QUERY_INFORMATION,
                    FALSE, pid);
    if (process == nullptr)
    {
        std::printf("cannot open process %lu: error %lu\n", pid, GetLastError());
        return false;
    }

    bool loaded = false;
    const SIZE_T size = dllPath.size() + 1;
    void* const remote = VirtualAllocEx(process, nullptr, size, MEM_COMMIT, PAGE_READWRITE);
    if (remote == nullptr)
    {
        std::printf("cannot allocate in the game: error %lu\n", GetLastError());
    }
    else if (WriteProcessMemory(process, remote, dllPath.c_str(), size, nullptr) == FALSE)
    {
        std::printf("cannot write the path into the game: error %lu\n", GetLastError());
    }
    else
    {
        // LoadLibraryA takes a pointer and returns a handle, which is close enough to a thread
        // entry point on Win32 for the loader to start it - but not close enough for the
        // compiler, hence the trip through void*.
        const auto entry = reinterpret_cast<void*>(
            GetProcAddress(GetModuleHandleA("kernel32.dll"), "LoadLibraryA"));
        const auto loadLibrary = reinterpret_cast<LPTHREAD_START_ROUTINE>(entry);
        const HANDLE thread =
            CreateRemoteThread(process, nullptr, 0, loadLibrary, remote, 0, nullptr);
        if (thread == nullptr)
        {
            std::printf("cannot start the loading thread: error %lu\n", GetLastError());
        }
        else
        {
            WaitForSingleObject(thread, INFINITE);
            DWORD module = 0;
            GetExitCodeThread(thread, &module);
            CloseHandle(thread);
            loaded = module != 0;
            std::printf(loaded ? "loaded, module at 0x%08lx\n"
                               : "the game did not load the dll (LoadLibraryA returned %lu)\n",
                        module);
        }
    }

    if (remote != nullptr)
    {
        VirtualFreeEx(process, remote, 0, MEM_RELEASE);
    }
    CloseHandle(process);
    return loaded;
}

} // namespace

int main(int argc, char** argv)
{
    if (argc < 2)
    {
        std::printf("usage: inject <path to hota-twitch.dll> [--process NAME] [--wait SECONDS]\n");
        return 2;
    }

    std::string dllPath = argv[1];
    const char* processName = kDefaultProcess;
    DWORD waitSeconds = 60;
    for (int index = 2; index + 1 < argc; index += 2)
    {
        if (std::strcmp(argv[index], "--process") == 0)
        {
            processName = argv[index + 1];
        }
        else if (std::strcmp(argv[index], "--wait") == 0)
        {
            waitSeconds = static_cast<DWORD>(std::atoi(argv[index + 1]));
        }
        else
        {
            std::printf("unknown option %s\n", argv[index]);
            return 2;
        }
    }

    char full[MAX_PATH]{};
    if (GetFullPathNameA(dllPath.c_str(), MAX_PATH, full, nullptr) != 0)
    {
        dllPath = full;
    }
    if (GetFileAttributesA(dllPath.c_str()) == INVALID_FILE_ATTRIBUTES)
    {
        std::printf("no such file: %s\n", dllPath.c_str());
        return 2;
    }

    std::printf("waiting for \"%s\" (up to %lu s)\n", processName, waitSeconds);
    const DWORD deadline = GetTickCount() + waitSeconds * 1000;
    for (;;)
    {
        const DWORD pid = findProcess(processName);
        if (pid != 0)
        {
            std::printf("found pid %lu, loading %s\n", pid, dllPath.c_str());
            return loadInto(pid, dllPath) ? 0 : 1;
        }
        if (GetTickCount() >= deadline)
        {
            std::printf("the game did not start in time\n");
            return 1;
        }
        Sleep(kPollIntervalMs);
    }
}
