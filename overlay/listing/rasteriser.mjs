import { encodeOpaquePng } from './png.mjs';

const PAGE = 'data:text/html,<meta charset="utf-8">';

const dataUrl = (type, bytes) => `data:${type};base64,${Buffer.from(bytes).toString('base64')}`;

/**
 * Draws images at exact pixel sizes through the same browser the screenshots come from, so the
 * listing needs no image library of its own. Everything it produces is opaque: the assets are
 * flat drawings on a solid ground, which is what Twitch asks for on the discovery image.
 */
export async function createRasteriser(browser, ground) {
  const page = await browser.newPage();
  await page.goto(PAGE);

  const draw = async (source, width, height) => {
    const base64 = await page.evaluate(
      async ([url, w, h, fill]) => {
        const image = new Image();
        image.src = url;
        await image.decode();
        const canvas = document.createElement('canvas');
        canvas.width = w;
        canvas.height = h;
        const context = canvas.getContext('2d');
        context.imageSmoothingEnabled = true;
        context.imageSmoothingQuality = 'high';
        context.fillStyle = fill;
        context.fillRect(0, 0, w, h);
        context.drawImage(image, 0, 0, w, h);
        const pixels = context.getImageData(0, 0, w, h).data;
        let binary = '';
        for (let i = 0; i < pixels.length; i += 1) binary += String.fromCharCode(pixels[i]);
        return btoa(binary);
      },
      [source, width, height, ground],
    );
    return encodeOpaquePng(width, height, Buffer.from(base64, 'base64'));
  };

  return {
    /** An SVG source at the exact size the console demands. */
    svg: (svg, width, height) => draw(dataUrl('image/svg+xml', svg), width, height),
    /** A captured frame brought down to the size the console demands. */
    fit: (png, width, height) => draw(dataUrl('image/png', png), width, height),
    /**
     * Whether a family is installed. A missing one is substituted without a word, which would
     * change an asset that is meant to be the same drawing every time it is rendered.
     */
    hasFont: (family) =>
      page.evaluate((name) => {
        const context = document.createElement('canvas').getContext('2d');
        const of = (stack) => {
          context.font = `bold 72px ${stack}`;
          return context.measureText('HotA Overlay').width;
        };
        return of(`'${name}', monospace`) !== of("'a family that is not installed', monospace");
      }, family),
  };
}
