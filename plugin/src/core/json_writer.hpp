#pragma once

#include <cstdint>
#include <string>
#include <string_view>

namespace hota_twitch
{

/// Appends a JSON document to a caller-owned string. Separators and quoting are the writer's
/// business; the caller only states structure. Strings must already be valid UTF-8.
class JsonWriter
{
public:
    explicit JsonWriter(std::string& out) noexcept;

    void beginObject();
    void endObject();
    void beginArray();
    void endArray();

    void key(std::string_view name);
    void string(std::string_view text);
    void number(std::int64_t value);
    void boolean(bool value);
    void nullValue();

private:
    void separate();
    void writeEscaped(std::string_view text);

    std::string& m_out;
    bool m_needComma = false;
};

} // namespace hota_twitch
