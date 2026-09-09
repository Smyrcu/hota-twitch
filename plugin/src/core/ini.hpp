#pragma once

#include <string>
#include <string_view>
#include <vector>

namespace hota_twitch
{

/// A parsed INI file: `[section]` headers, `key = value` lines, `;` and `#` comments.
/// Section and key names are matched case-insensitively; values keep their case and are
/// trimmed of surrounding whitespace.
class IniDocument
{
public:
    static IniDocument parse(std::string_view contents);

    /// The value of `section.key`, or `fallback` when the file does not set it.
    std::string_view value(std::string_view section, std::string_view key,
                           std::string_view fallback = {}) const;

    bool has(std::string_view section, std::string_view key) const;

private:
    struct Entry
    {
        std::string section;
        std::string key;
        std::string value;
    };

    const Entry* find(std::string_view section, std::string_view key) const;

    std::vector<Entry> m_entries;
};

} // namespace hota_twitch
