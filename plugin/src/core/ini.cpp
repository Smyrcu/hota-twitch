#include "ini.hpp"

#include <algorithm>
#include <cctype>

namespace hota_twitch
{
namespace
{

std::string_view trim(std::string_view text)
{
    const auto isSpace = [](char c) {
        return std::isspace(static_cast<unsigned char>(c)) != 0;
    };
    while (!text.empty() && isSpace(text.front()))
    {
        text.remove_prefix(1);
    }
    while (!text.empty() && isSpace(text.back()))
    {
        text.remove_suffix(1);
    }
    return text;
}

bool equalsIgnoreCase(std::string_view left, std::string_view right)
{
    return left.size() == right.size() &&
           std::equal(left.begin(), left.end(), right.begin(), [](char a, char b) {
               return std::tolower(static_cast<unsigned char>(a)) ==
                      std::tolower(static_cast<unsigned char>(b));
           });
}

} // namespace

IniDocument IniDocument::parse(std::string_view contents)
{
    IniDocument document;
    std::string section;
    while (!contents.empty())
    {
        const std::size_t breakAt = contents.find('\n');
        std::string_view line = contents.substr(0, breakAt);
        contents = breakAt == std::string_view::npos ? std::string_view{}
                                                     : contents.substr(breakAt + 1);
        if (!line.empty() && line.back() == '\r')
        {
            line.remove_suffix(1);
        }
        line = trim(line);
        if (line.empty() || line.front() == ';' || line.front() == '#')
        {
            continue;
        }
        if (line.front() == '[')
        {
            const std::size_t close = line.find(']');
            if (close != std::string_view::npos)
            {
                section = trim(line.substr(1, close - 1));
            }
            continue;
        }
        const std::size_t separator = line.find('=');
        if (separator == std::string_view::npos)
        {
            continue;
        }
        const std::string_view key = trim(line.substr(0, separator));
        if (key.empty())
        {
            continue;
        }
        document.m_entries.push_back(
            {section, std::string(key), std::string(trim(line.substr(separator + 1)))});
    }
    return document;
}

std::string_view IniDocument::value(std::string_view section, std::string_view key,
                                    std::string_view fallback) const
{
    const Entry* entry = find(section, key);
    return entry == nullptr ? fallback : std::string_view(entry->value);
}

bool IniDocument::has(std::string_view section, std::string_view key) const
{
    return find(section, key) != nullptr;
}

const IniDocument::Entry* IniDocument::find(std::string_view section, std::string_view key) const
{
    for (const Entry& entry : m_entries)
    {
        if (equalsIgnoreCase(entry.section, section) && equalsIgnoreCase(entry.key, key))
        {
            return &entry;
        }
    }
    return nullptr;
}

} // namespace hota_twitch
