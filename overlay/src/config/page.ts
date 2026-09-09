import { twitchExt, type TwitchAuth } from '../twitch/ext.js';
import { ConfigApi } from './api.js';
import { describeConnection } from './status.js';

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
  private readonly view: StatusView;
  private readonly controls: TokenControls | null;

  constructor(root: ParentNode) {
    this.view = collectStatus(root);
    this.controls = collectControls(root);
    if (this.controls === null) return;
    this.controls.generate.addEventListener('click', () => void this.generate());
    this.controls.revoke.addEventListener('click', () => void this.revoke());
    this.controls.copy.addEventListener('click', () => void this.copy());
  }

  start(): void {
    const ext = twitchExt();
    if (ext === null) {
      this.view.status.textContent = 'Open this page from the Twitch dashboard.';
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
      this.view.status.textContent = status.text;
      this.view.status.classList.toggle('connected', status.connected);
      this.view.hint.textContent = channel.tokenHint ?? '';
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

  private fail(failed: boolean): void {
    this.view.error.textContent = failed ? 'The backend did not answer. Try again in a moment.' : '';
    this.view.error.hidden = !failed;
  }
}

export function startConfig(root: ParentNode): void {
  new ConfigPage(root).start();
}
