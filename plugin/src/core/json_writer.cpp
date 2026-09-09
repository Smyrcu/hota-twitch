#include "json_writer.hpp"

#include <array>
#include <cstdio>

namespace hota_twitch
{
namespace
{

constexpr char kHexDigits[] = "0123456789abcdef";

} // namespace

JsonWriter::JsonWriter(std::string& out) noexcept : m_out(out)
{
}

void JsonWriter::beginObject()
{
    separate();
    m_out += '{';
    m_needComma = false;
}

void JsonWriter::endObject()
{
    m_out += '}';
    m_needComma = true;
}

void JsonWriter::beginArray()
{
    separate();
    m_out += '[';
    m_needComma = false;
}

void JsonWriter::endArray()
{
    m_out += ']';
    m_needComma = true;
}

void JsonWriter::key(std::string_view name)
{
    separate();
    writeEscaped(name);
    m_out += ':';
    m_needComma = false;
}

void JsonWriter::string(std::string_view text)
{
    separate();
    writeEscaped(text);
    m_needComma = true;
}

void JsonWriter::number(std::int64_t value)
{
    separate();
    std::array<char, 24> buffer{};
    const int written = std::snprintf(buffer.data(), buffer.size(), "%lld",
                                      static_cast<long long>(value));
    if (written > 0)
    {
        m_out.append(buffer.data(), static_cast<std::size_t>(written));
    }
    m_needComma = true;
}

void JsonWriter::boolean(bool value)
{
    separate();
    m_out += value ? "true" : "false";
    m_needComma = true;
}

void JsonWriter::nullValue()
{
    separate();
    m_out += "null";
    m_needComma = true;
}

void JsonWriter::separate()
{
    if (m_needComma)
    {
        m_out += ',';
    }
}

void JsonWriter::writeEscaped(std::string_view text)
{
    m_out += '"';
    for (const char raw : text)
    {
        const auto byte = static_cast<unsigned char>(raw);
        switch (byte)
        {
        case '"':
            m_out += "\\\"";
            break;
        case '\\':
            m_out += "\\\\";
            break;
        case '\b':
            m_out += "\\b";
            break;
        case '\f':
            m_out += "\\f";
            break;
        case '\n':
            m_out += "\\n";
            break;
        case '\r':
            m_out += "\\r";
            break;
        case '\t':
            m_out += "\\t";
            break;
        default:
            if (byte < 0x20)
            {
                m_out += "\\u00";
                m_out += kHexDigits[byte >> 4];
                m_out += kHexDigits[byte & 0x0F];
            }
            else
            {
                m_out += raw;
            }
            break;
        }
    }
    m_out += '"';
}

} // namespace hota_twitch
