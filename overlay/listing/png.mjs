import { deflateSync, inflateSync } from 'node:zlib';

const SIGNATURE = Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);

/** Truecolour without an alpha channel: the value Twitch's "avoid transparency" tip asks for. */
export const RGB = 2;

const CRC_TABLE = (() => {
  const table = new Int32Array(256);
  for (let n = 0; n < 256; n += 1) {
    let c = n;
    for (let k = 0; k < 8; k += 1) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
    table[n] = c;
  }
  return table;
})();

function crc32(...parts) {
  let c = -1;
  for (const part of parts) {
    for (const byte of part) c = CRC_TABLE[(c ^ byte) & 0xff] ^ (c >>> 8);
  }
  return (c ^ -1) >>> 0;
}

function chunk(type, data) {
  const head = Buffer.alloc(8);
  head.writeUInt32BE(data.length, 0);
  head.write(type, 4, 'ascii');
  const crc = Buffer.alloc(4);
  crc.writeUInt32BE(crc32(head.subarray(4), data), 0);
  return Buffer.concat([head, data, crc]);
}

/**
 * Writes opaque pixels as a PNG without an alpha channel. The pixels come from a canvas as RGBA;
 * dropping the alpha here rather than exporting the canvas means the file provably holds no
 * transparency instead of holding pixels that merely happen to be opaque.
 */
export function encodeOpaquePng(width, height, rgba) {
  const stride = width * 3;
  // One byte per scanline says which filter it uses; zero is None, which is what Buffer.alloc left.
  const raw = Buffer.alloc((stride + 1) * height);
  for (let y = 0; y < height; y += 1) {
    for (let x = 0; x < width; x += 1) {
      const from = (y * width + x) * 4;
      const to = y * (stride + 1) + 1 + x * 3;
      raw[to] = rgba[from];
      raw[to + 1] = rgba[from + 1];
      raw[to + 2] = rgba[from + 2];
    }
  }

  const header = Buffer.alloc(13);
  header.writeUInt32BE(width, 0);
  header.writeUInt32BE(height, 4);
  header[8] = 8;
  header[9] = RGB;
  return Buffer.concat([
    SIGNATURE,
    chunk('IHDR', header),
    chunk('IDAT', deflateSync(raw, { level: 9 })),
    chunk('IEND', Buffer.alloc(0)),
  ]);
}

/** Where the pixels of an unfiltered truecolour image start in each scanline of the raw data. */
export const scanline = (width) => width * 3 + 1;

/**
 * The size and colour type a PNG declares, or null for anything that is not one. A file that was
 * written short reads as not a PNG rather than throwing, so the checks can name it.
 */
export function readHeader(file) {
  if (file.length < 26) return null;
  if (!file.subarray(0, 8).equals(SIGNATURE)) return null;
  if (file.readUInt32BE(8) !== 13 || file.toString('ascii', 12, 16) !== 'IHDR') return null;
  return {
    width: file.readUInt32BE(16),
    height: file.readUInt32BE(20),
    colourType: file[25],
  };
}

/**
 * How many bytes of image data the file really holds, or null when it cannot be read. The header
 * only states a size; this is what says the pixels behind it are that size too.
 */
export function readPixelBytes(file) {
  const parts = [];
  let at = 8;
  while (at + 8 <= file.length) {
    const length = file.readUInt32BE(at);
    const type = file.toString('ascii', at + 4, at + 8);
    if (at + 12 + length > file.length) return null;
    if (type === 'IDAT') parts.push(file.subarray(at + 8, at + 8 + length));
    if (type === 'IEND') break;
    at += 12 + length;
  }
  if (parts.length === 0) return null;
  try {
    return inflateSync(Buffer.concat(parts)).length;
  } catch {
    return null;
  }
}
