import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AuthService } from '../../core/auth.service';
import { ChatMessage, ChatService } from '../../core/chat.service';
import { errorMessage } from '../../core/util';

const PAGE_SIZE = 50; // matches the API

/** Conversations at /messages, with the open thread at /messages/:id. */
@Component({
  selector: 'app-messages-page',
  imports: [RouterLink, DatePipe],
  templateUrl: './messages-page.html',
})
export class MessagesPage {
  private http = inject(HttpClient);
  protected chat = inject(ChatService);
  protected auth = inject(AuthService);
  id = input<string>(); // route param, absent on /messages

  protected current = computed(() => this.chat.conversations().find((c) => c.id === this.id()));
  protected thread = signal<ChatMessage[]>([]);
  protected hasOlder = signal(false);
  protected loading = signal(false);
  protected draft = signal('');
  protected sending = signal(false);
  protected failure = signal<string | null>(null);

  constructor() {
    effect(() => {
      const id = this.id();
      untracked(() => void this.open(id));
    });
    this.chat.messages.pipe(takeUntilDestroyed()).subscribe((m) => {
      if (m.conversationId === this.id()) this.add([m]);
    });
    inject(DestroyRef).onDestroy(() => this.chat.openId.set(null));
  }

  private async open(id: string | undefined) {
    this.chat.openId.set(id ?? null);
    this.thread.set([]);
    this.draft.set('');
    this.failure.set(null);
    if (!id) return;
    this.loading.set(true);
    try {
      const page = await firstValueFrom(this.http.get<ChatMessage[]>(`/api/conversations/${id}/messages`));
      if (id !== this.id()) return; // switched threads meanwhile
      this.add(page);
      this.hasOlder.set(page.length === PAGE_SIZE);
      this.chat.markRead(id);
    } catch (e) {
      this.failure.set(e instanceof HttpErrorResponse && e.status === 404 ? 'Conversation not found.' : errorMessage(e));
    } finally {
      this.loading.set(false);
    }
  }

  protected async loadOlder() {
    const id = this.id();
    const oldest = this.thread()[0];
    if (!id || !oldest) return;
    this.loading.set(true);
    try {
      const page = await firstValueFrom(
        this.http.get<ChatMessage[]>(`/api/conversations/${id}/messages`, { params: { before: oldest.id } }),
      );
      this.add(page);
      this.hasOlder.set(page.length === PAGE_SIZE);
    } catch (e) {
      this.failure.set(errorMessage(e));
    } finally {
      this.loading.set(false);
    }
  }

  protected async send(event: Event) {
    event.preventDefault();
    const id = this.id();
    const body = this.draft().trim();
    if (!id || !body || this.sending()) return;
    this.sending.set(true);
    this.failure.set(null);
    try {
      this.add([await firstValueFrom(this.http.post<ChatMessage>(`/api/conversations/${id}/messages`, { body }))]);
      this.draft.set('');
    } catch (e) {
      this.failure.set(errorMessage(e));
    } finally {
      this.sending.set(false);
    }
  }

  /** Merges by id: your own message arrives twice (the POST response and the hub push), in either order. */
  private add(messages: ChatMessage[]) {
    this.thread.update((t) => {
      const seen = new Set(t.map((m) => m.id));
      return [...t, ...messages.filter((m) => !seen.has(m.id))].sort((a, b) => a.id - b.id);
    });
  }
}
