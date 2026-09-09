#pragma once

#include "codepage.hpp"
#include "snapshot.hpp"

#include <string>

namespace hota_twitch
{

/// Writes the snapshot as the state document of `docs/protocol.md` (version 1) into `out`,
/// replacing whatever it held. Names are converted from `codepage` to UTF-8 here rather than
/// when the snapshot is taken, so the conversion happens on the worker thread.
void serializeState(const StateSnapshot& state, Codepage codepage, std::string& out);

} // namespace hota_twitch
