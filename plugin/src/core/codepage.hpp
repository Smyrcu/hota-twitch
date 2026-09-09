#pragma once

#include <string>
#include <string_view>

namespace hota_twitch
{

/// Single-byte code page the game's text is stored in. The English and Western builds use
/// Windows-1252, the Polish/Central European ones Windows-1250, the Russian ones Windows-1251.
enum class Codepage
{
    Windows1250,
    Windows1251,
    Windows1252,
};

/// Parses a code page written as a number ("1250") or with a prefix ("cp1251", "windows-1252").
/// Returns false and leaves `out` untouched when the text names no supported code page.
bool parseCodepage(std::string_view text, Codepage& out);

/// Converts single-byte text to UTF-8. Bytes with no mapping become U+FFFD.
std::string toUtf8(std::string_view text, Codepage codepage);

/// The text up to the first NUL, for the fixed-size char arrays the game stores names in.
std::string_view untilNul(const char* text, std::size_t capacity);

} // namespace hota_twitch
