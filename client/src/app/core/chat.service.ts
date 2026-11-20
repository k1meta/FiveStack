import { HttpClient } from '@angular/common/http';
import { Injectable, computed, effect, inject, signal, untracked } from '@angular/core';
import type { HubConnection } from '@microsoft/signalr';
import { Subject, firstValueFrom } from 'rxjs';
import type { PublicProfile } from '../features/profile/profile-page';
import { AuthService } from './auth.service';

export interface ChatMessage {
  id: number;
  conversationId: string;
  senderId: string;
  senderName: string;
  body: string;
  sentAt: string;
}

export interface Conversation {
  id: string;
  /** Set for a squad chat; null for a DM. */
  squadPostId: string | null;
  title: string;
  /** The other player in a DM. */
  other: PublicProfile | null;
  last: ChatMessage | null;
  unread: number;
}

/**
 * Chat state for the signed-in player: the conversation list with unread counts, plus the SignalR connection
 * that pushes new messages (`MessageReceived`) and squad request changes (`SquadChanged`). Messages are sent over REST.
 */
@Injectable({ providedIn: 'root' })
export class ChatService {
  private http = inject(HttpClient);
  private auth = inject(AuthService);
  private hub: HubConnection | null = null;
  private userId = computed(() => this.auth.user()?.id ?? null);

  readonly conversations = signal<Conversation[]>([]);
  readonly unread = computed(() => this.conversations().reduce((n, c) => n + c.unread, 0));
  /** The thread on screen: its incoming messages are marked read right away. */
  readonly openId = signal<string | null>(null);
  /** Every message pushed to this player, in any conversation. */
  readonly messages = new Subject<ChatMessage>();
  /** A squad post id whose requests or roster changed. */
  readonly squadChanged = new Subject<string>();

  constructor() {
    // Connect on sign-in, disconnect on sign-out.
    effect(() => {
      const id = this.userId();
      untracked(() => void (id ? this.start() : this.stop()));
    });
  }

  async load() {
    try {
      this.conversations.set(await firstValueFrom(this.http.get<Conversation[]>('/api/conversations')));
    } catch {
      // keep the last list; the next push or page load retries
    }
  }

  /** Finds or starts the DM with a player and returns its id. */
  async startDm(userId: string) {
    const { id } = await firstValueFrom(this.http.post<{ id: string }>(`/api/conversations/dm/${userId}`, null));
    await this.load();
    return id;
  }

  markRead(id: string) {
    this.conversations.update((list) => list.map((c) => (c.id === id ? { ...c, unread: 0 } : c)));
    this.http.post(`/api/conversations/${id}/read`, null).subscribe({ error: () => { /* best effort: the badge is already cleared locally */ } });
  }

  private async start() {
    await this.stop();
    const { HubConnectionBuilder } = await import('@microsoft/signalr'); // signed-out visitors never download it
    const hub = new HubConnectionBuilder().withUrl('/hubs/chat').withAutomaticReconnect().build();
    hub.on('MessageReceived', (m: ChatMessage) => this.received(m));
    hub.on('SquadChanged', (postId: string) => {
      this.squadChanged.next(postId);
      void this.load(); // an accept adds a squad chat
    });
    hub.onreconnected(() => void this.load()); // pushes sent while offline are lost
    this.hub = hub;
    await Promise.all([hub.start().catch((e) => console.warn('Chat hub unavailable', e)), this.load()]);
  }

  private async stop() {
    const hub = this.hub;
    this.hub = null;
    this.conversations.set([]);
    await hub?.stop();
  }

  private received(m: ChatMessage) {
    const known = this.conversations().find((c) => c.id === m.conversationId);
    if (!known) {
      void this.load(); // a new DM from someone
    } else {
      const read = m.senderId === this.userId() || m.conversationId === this.openId();
      const updated = { ...known, last: m, unread: read ? known.unread : known.unread + 1 };
      this.conversations.update((list) => [updated, ...list.filter((c) => c.id !== m.conversationId)]);
      if (read && m.senderId !== this.userId()) this.markRead(m.conversationId);
    }
    this.messages.next(m);
  }
}
