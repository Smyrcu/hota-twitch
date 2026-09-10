import { twitchExt, type TwitchAuth } from '../twitch/ext.js';
import { ConfigApi } from './api.js';
import { DEFAULT_UI_SCALE, formatUiScale, settleUiScale, uiScaleOptions } from './settings.js';
import { describeConnection } from './status.js';
import { Typography } from './typography.js';

const REFRESH_MS = 10_000;

interface StatusView {
  readonly status: HTMLElement;
  readonly hint: HTMLElement;
  readonly error: HTMLElement;
}

interface TokenControls {
  readonly token: HTMLElement;
  readonly tokenValue: HTMLElement;
  readonly generate: HTMLButtonElement;
  readonly revoke: HTMLButtonElement;
  readonly copy: HTMLButtonElement;
}

function required<T extends HTMLElement>(root: ParentNode, selector: string): T {
  const element = root.querySelector<T>(selector);
  if (element === null) throw new Error(`missing element ${selector}`);
  return element;
}

function collectStatus(root: ParentNode): StatusView {
  return {
    status: required(root, '[data-status]'),
    hint: required(root, '[data-hint]'),
    error: required(root, '[data-error]'),
  };
}

/** `live_config.html` carries only the status markup, so the controls are optional. */
function collectControls(root: ParentNode): TokenControls | null {
  if (root.querySelector('[data-generate]') === null) return null;
  return {
    token: required(root, '[data-token]'),
    tokenValue: required(root, '[data-token-value]'),
    generate: required(root, '[data-generate]'),
    revoke: required(root, '[data-revoke]'),
    copy: required(root, '[data-copy]'),
  };
}

/** The streamer pages: the token lives here, the connection status comes from the backend. */
export class ConfigPage {
  private api: ConfigApi | null = null;
  private polling: ReturnType<typeof setInterval> | null = null;
  private refreshing = false;
  /** A scale the streamer picked that the backend has not confirmed yet. */
  private chosenScale: number | null = null;
  private readonly view: StatusView;
  private readonly controls: TokenControls | null;
  private readonly uiScale: HTMLSelectElement | null;
  private readonly type = new Typography();

  constructor(private readonly root: ParentNode) {
    this.view = collectStatus(root);
    this.controls = collectControls(root);
    this.uiScale = root.querySelector<HTMLSelectElement>('[data-ui-scale]');
    if (this.uiScale !== null) {
      this.showUiScale(DEFAULT_UI_SCALE);
      this.uiScale.addEventListener('change', () => void this.saveUiScale());
    }
    if (this.controls === null) return;
    this.controls.generate.addEventListener('click', () => void this.generate());
    this.controls.revoke.addEventListener('click', () => void this.revoke());
    this.controls.copy.addEventListener('click', () => void this.copy());
  }

  async start(): Promise<void> {
    await this.type.start(this.root);
    const ext = twitchExt();
    if (ext === null) {
      this.type.write(this.view.status, 'Open this page from the Twitch dashboard.');
      return;
    }
    ext.onAuthorized((auth: TwitchAuth) => {
      this.api = new ConfigApi(auth.token);
      void this.refresh();
      if (this.polling === null) this.polling = setInterval(() => void this.refresh(), REFRESH_MS);
    });
  }

  private async refresh(): Promise<void> {
    if (this.api === null || this.refreshing) return;
    this.refreshing = true;
    try {
      const channel = await this.api.channel();
      const status = describeConnection(channel.hasToken, channel.lastStateAt);
      this.type.write(this.view.status, status.text, status.connected ? 'good' : 'body');
      // The class still carries the meaning when the game fonts cannot be loaded.
      this.view.status.classList.toggle('connected', status.connected);
      this.type.write(this.view.hint, channel.tokenHint ?? '');
      const scale = settleUiScale(this.chosenScale, channel.settings.uiScale);
      this.chosenScale = scale.chosen;
      this.showUiScale(scale.show);
      if (this.controls !== null) {
        this.controls.revoke.disabled = !channel.hasToken;
        this.controls.generate.textContent = channel.hasToken ? 'Generate a new token' : 'Generate token';
      }
      this.fail(false);
    } catch {
      this.fail(true);
    } finally {
      this.refreshing = false;
    }
  }

  private async generate(): Promise<void> {
    const controls = this.controls;
    if (this.api === null || controls === null) return;
    try {
      controls.tokenValue.textContent = await this.api.issueToken();
      controls.token.hidden = false;
      controls.copy.textContent = 'Copy';
      this.fail(false);
      await this.refresh();
    } catch {
      this.fail(true);
    }
  }

  private async revoke(): Promise<void> {
    const controls = this.controls;
    if (this.api === null || controls === null) return;
    try {
      await this.api.revokeToken();
      controls.token.hidden = true;
      controls.tokenValue.textContent = '';
      this.fail(false);
      await this.refresh();
    } catch {
      this.fail(true);
    }
  }

  /** The channel keeps its own scale, so the control works before a token exists. */
  private async saveUiScale(): Promise<void> {
    const select = this.uiScale;
    if (this.api === null || select === null) return;
    const uiScale = Number(select.value);
    this.chosenScale = uiScale;
    try {
      await this.api.saveSettings({ uiScale });
      this.fail(false);
    } catch {
      // The next poll brings back what is actually stored, so the control cannot claim otherwise.
      this.chosenScale = null;
      this.fail(true, 'The interface scale could not be saved.');
    }
  }

  /** A channel may hold a scale outside the listed steps; offer it rather than misreport it. */
  private showUiScale(value: number): void {
    const select = this.uiScale;
    if (select === null) return;
    const steps = uiScaleOptions(value).map(formatUiScale);
    if ([...select.options].map((option) => option.value).join() !== steps.join()) {
      select.replaceChildren(...steps.map((step) => new Option(step, step)));
    }
    select.value = formatUiScale(value);
  }

  private async copy(): Promise<void> {
    const controls = this.controls;
    if (controls === null) return;
    const token = controls.tokenValue.textContent ?? '';
    if (token === '') return;
    try {
      await navigator.clipboard.writeText(token);
      controls.copy.textContent = 'Copied';
    } catch {
      controls.copy.textContent = 'Copy failed, select it by hand';
    }
  }

  /** Unhidden first: a hidden element has no width, so there is nothing to lay the text into. */
  private fail(failed: boolean, reason = 'The backend did not answer. Try again in a moment.'): void {
    this.view.error.hidden = !failed;
    this.type.write(this.view.error, failed ? reason : '');
  }
}

export function startConfig(root: ParentNode): void {
  void new ConfigPage(root).start();
}
