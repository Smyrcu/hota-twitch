#pragma once

#include "core/logging.hpp"

#include <string>

#include <windows.h>
#include <winhttp.h>

namespace hota_twitch::platform
{

/// Posts state documents to the backend over WinHTTP, which is what the game's own Windows
/// build has and what works unchanged under Wine and Proton.
class HttpClient
{
public:
    explicit HttpClient(Logger& log);
    ~HttpClient();

    HttpClient(const HttpClient&) = delete;
    HttpClient& operator=(const HttpClient&) = delete;

    /// Resolves the backend address and keeps the connection for the life of the plugin.
    /// `url` is the backend root; the state endpoint is appended to it.
    bool open(const std::string& url, const std::string& token);

    /// Sends one document. Returns the HTTP status the backend answered with, or 0 when the
    /// request never got that far.
    int postState(const std::string& body);

private:
    void close();

    Logger& m_log;
    HINTERNET m_session = nullptr;
    HINTERNET m_connection = nullptr;
    std::wstring m_path;
    std::wstring m_headers;
    bool m_secure = false;
};

} // namespace hota_twitch::platform
