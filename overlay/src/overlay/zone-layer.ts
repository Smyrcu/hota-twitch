import type { Zone } from '../zones/index.js';

export interface ZoneHandlers {
  readonly onEnter: (zone: Zone) => void;
  readonly onLeave: (zone: Zone) => void;
  readonly onClick: (zone: Zone) => void;
}

/**
 * The hover targets over the panel. The layer itself never takes the pointer; only the zones
 * do, so a click anywhere else still reaches the player.
 */
export class ZoneLayer {
  private readonly elements: HTMLDivElement[] = [];
  private zones: readonly Zone[] = [];

  constructor(
    private readonly root: HTMLElement,
    private readonly handlers: ZoneHandlers,
  ) {}

  render(zones: readonly Zone[], debug: boolean): void {
    while (this.elements.length < zones.length) this.elements.push(this.create());
    this.elements.forEach((element, index) => {
      const zone = zones[index];
      if (zone === undefined) {
        element.hidden = true;
        return;
      }
      element.hidden = false;
      element.dataset['index'] = String(index);
      element.classList.toggle('debug', debug);
      element.style.left = `${zone.rect.x}px`;
      element.style.top = `${zone.rect.y}px`;
      element.style.width = `${zone.rect.width}px`;
      element.style.height = `${zone.rect.height}px`;
    });
    this.zones = zones;
  }

  private create(): HTMLDivElement {
    const element = document.createElement('div');
    element.className = 'zone';
    element.addEventListener('pointerenter', () => this.dispatch(element, this.handlers.onEnter));
    element.addEventListener('pointerleave', () => this.dispatch(element, this.handlers.onLeave));
    element.addEventListener('click', () => this.dispatch(element, this.handlers.onClick));
    this.root.appendChild(element);
    return element;
  }

  private dispatch(element: HTMLDivElement, handler: (zone: Zone) => void): void {
    const index = Number(element.dataset['index']);
    const zone = this.zones[index];
    if (zone !== undefined) handler(zone);
  }
}
