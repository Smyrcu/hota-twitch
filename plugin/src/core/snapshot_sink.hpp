#pragma once

#include "snapshot.hpp"

namespace hota_twitch
{

/// Where the hooks hand their snapshots. Keeping this an interface is what lets the plugin
/// start the worker thread on the first snapshot instead of in `DllMain`, where creating a
/// thread under the loader lock can deadlock the game.
class SnapshotSink
{
public:
    virtual ~SnapshotSink() = default;

    /// Takes ownership of the snapshot's contents, leaving `state` holding whatever the sink
    /// had before, so the caller can fill it again without allocating.
    virtual void publish(StateSnapshot& state) = 0;
};

} // namespace hota_twitch
