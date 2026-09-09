#include "http_client.hpp"

#include <vector>

namespace hota_twitch::platform
{
namespace
{

/// `docs/protocol.md` section 2.
constexpr const wchar_t* kStatePath = L"/v1/state";

/// A stream should never stall because the backend is slow; every phase gets the same short
/// budget and a missed post is simply retried on the next tick.
constexpr DWORD kTimeoutMs = 5000;

std::wstring widen(const std::string& text)
{
    if (text.empty())
    {
        return {};
    }
    const int size = MultiByteToWideChar(CP_UTF8, 0, text.c_str(), static_cast<int>(text.size()),
                                         nullptr, 0);
    if (size <= 0)
    {
        return {};
    }
    std::wstring wide(static_cast<std::size_t>(size), L'\0');
    MultiByteToWideChar(CP_UTF8, 0, text.c_str(), static_cast<int>(text.size()), wide.data(),
                        size);
    return wide;
}

} // namespace

HttpClient::HttpClient(Logger& log) : m_log(log)
{
}

HttpClient::~HttpClient()
{
    close();
}

bool HttpClient::open(const std::string& url, const std::string& token)
{
    close();

    const std::wstring wideUrl = widen(url);
    std::vector<wchar_t> host(256);
    URL_COMPONENTS parts{};
    parts.dwStructSize = sizeof(parts);
    parts.lpszHostName = host.data();
    parts.dwHostNameLength = static_cast<DWORD>(host.size());
    parts.dwUrlPathLength = static_cast<DWORD>(-1);
    if (WinHttpCrackUrl(wideUrl.c_str(), static_cast<DWORD>(wideUrl.size()), 0, &parts) == FALSE)
    {
        m_log.error("the backend url in hota-twitch.ini is not an address WinHTTP understands");
        return false;
    }

    m_secure = parts.nScheme == INTERNET_SCHEME_HTTPS;
    std::wstring root(parts.lpszUrlPath, parts.dwUrlPathLength);
    while (!root.empty() && root.back() == L'/')
    {
        root.pop_back();
    }
    m_path = root + kStatePath;
    m_headers = L"Content-Type: application/json\r\nAuthorization: Bearer " + widen(token);

    m_session = WinHttpOpen(L"hota-twitch", WINHTTP_ACCESS_TYPE_AUTOMATIC_PROXY,
                            WINHTTP_NO_PROXY_NAME, WINHTTP_NO_PROXY_BYPASS, 0);
    if (m_session == nullptr)
    {
        m_log.error("WinHTTP could not be started, error " + std::to_string(GetLastError()));
        return false;
    }
    WinHttpSetTimeouts(m_session, kTimeoutMs, kTimeoutMs, kTimeoutMs, kTimeoutMs);

    m_connection = WinHttpConnect(m_session, std::wstring(parts.lpszHostName,
                                                          parts.dwHostNameLength)
                                                 .c_str(),
                                  parts.nPort, 0);
    if (m_connection == nullptr)
    {
        m_log.error("the backend host could not be resolved, error " +
                    std::to_string(GetLastError()));
        close();
        return false;
    }
    return true;
}

int HttpClient::postState(const std::string& body)
{
    if (m_connection == nullptr)
    {
        return 0;
    }
    const HINTERNET request =
        WinHttpOpenRequest(m_connection, L"POST", m_path.c_str(), nullptr, WINHTTP_NO_REFERER,
                           WINHTTP_DEFAULT_ACCEPT_TYPES, m_secure ? WINHTTP_FLAG_SECURE : 0);
    if (request == nullptr)
    {
        m_log.warn("the request could not be built, error " + std::to_string(GetLastError()));
        return 0;
    }

    int status = 0;
    const BOOL sent =
        WinHttpSendRequest(request, m_headers.c_str(), static_cast<DWORD>(m_headers.size()),
                           const_cast<char*>(body.data()), static_cast<DWORD>(body.size()),
                           static_cast<DWORD>(body.size()), 0);
    if (sent != FALSE && WinHttpReceiveResponse(request, nullptr) != FALSE)
    {
        DWORD code = 0;
        DWORD size = sizeof(code);
        if (WinHttpQueryHeaders(request,
                                WINHTTP_QUERY_STATUS_CODE | WINHTTP_QUERY_FLAG_NUMBER,
                                WINHTTP_HEADER_NAME_BY_INDEX, &code, &size,
                                WINHTTP_NO_HEADER_INDEX) != FALSE)
        {
            status = static_cast<int>(code);
        }
    }
    else
    {
        m_log.warn("the backend could not be reached, error " + std::to_string(GetLastError()));
    }

    WinHttpCloseHandle(request);
    return status;
}

void HttpClient::close()
{
    if (m_connection != nullptr)
    {
        WinHttpCloseHandle(m_connection);
        m_connection = nullptr;
    }
    if (m_session != nullptr)
    {
        WinHttpCloseHandle(m_session);
        m_session = nullptr;
    }
}

} // namespace hota_twitch::platform
