#include "framework.hpp"

#include "core/json_writer.hpp"

using namespace hota_twitch;

HOTA_TEST(json_writes_a_flat_object)
{
    std::string out;
    JsonWriter json(out);
    json.beginObject();
    json.key("v");
    json.number(1);
    json.key("name");
    json.string("Todd");
    json.key("live");
    json.boolean(true);
    json.key("research");
    json.nullValue();
    json.endObject();

    HOTA_CHECK_EQ(out, std::string(R"({"v":1,"name":"Todd","live":true,"research":null})"));
}

HOTA_TEST(json_writes_nested_arrays)
{
    std::string out;
    JsonWriter json(out);
    json.beginObject();
    json.key("army");
    json.beginArray();
    json.beginArray();
    json.number(0);
    json.number(13);
    json.number(3);
    json.endArray();
    json.beginArray();
    json.number(1);
    json.number(110);
    json.number(15);
    json.endArray();
    json.endArray();
    json.key("empty");
    json.beginArray();
    json.endArray();
    json.endObject();

    HOTA_CHECK_EQ(out, std::string(R"({"army":[[0,13,3],[1,110,15]],"empty":[]})"));
}

HOTA_TEST(json_escapes_quotes_backslashes_and_whitespace)
{
    std::string out;
    JsonWriter json(out);
    json.string("a\"b\\c\nd\te");

    HOTA_CHECK_EQ(out, std::string(R"("a\"b\\c\nd\te")"));
}

HOTA_TEST(json_escapes_other_control_characters_as_unicode)
{
    std::string text = "x";
    text += static_cast<char>(1);
    text += 'y';
    text += static_cast<char>(31);

    std::string out;
    JsonWriter json(out);
    json.string(text);

    HOTA_CHECK_EQ(out, std::string("\"x") + "\\u0001" + "y" + "\\u001f" + "\"");
}

HOTA_TEST(json_passes_utf8_through_untouched)
{
    const std::string polish = "Wieza \xc5\x81odz";

    std::string out;
    JsonWriter json(out);
    json.string(polish);

    HOTA_CHECK_EQ(out, "\"" + polish + "\"");
}

HOTA_TEST(json_writes_negative_and_large_numbers)
{
    std::string out;
    JsonWriter json(out);
    json.beginArray();
    json.number(-1);
    json.number(1788907728157LL);
    json.endArray();

    HOTA_CHECK_EQ(out, std::string("[-1,1788907728157]"));
}
