const GZIP_PREFIX = 'gz:';

function base64ToBytes(value: string): Uint8Array<ArrayBuffer> {
    const binary = atob(value);
    const bytes = new Uint8Array(new ArrayBuffer(binary.length));
    for (let index = 0; index < binary.length; index += 1) bytes[index] = binary.charCodeAt(index);
    return bytes;
}

/**
 * Broadcast payloads are `gz:` followed by base64 of gzip(state JSON) (docs/protocol.md §3);
 * a payload without the prefix is plain JSON. Returns null for anything undecodable.
 */
export async function decodeBroadcast(message: string): Promise<string | null> {
    if (!message.startsWith(GZIP_PREFIX)) return message;
    try {
        const bytes = base64ToBytes(message.slice(GZIP_PREFIX.length));
        const stream = new Blob([bytes]).stream().pipeThrough(new DecompressionStream('gzip'));
        return await new Response(stream).text();
    } catch {
        return null;
    }
}
