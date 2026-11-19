import { createWorld } from '../src/server.js';

export async function startTestWorld(options = {}) {
  const world = createWorld(options);
  const { port } = await world.listen(0, '127.0.0.1');
  return {
    world,
    base: `http://127.0.0.1:${port}`,
    wsUrl: `ws://127.0.0.1:${port}`,
    stop: () => world.close()
  };
}

export async function post(base, path, body, token) {
  const response = await fetch(base + path, {
    method: 'POST',
    headers: {
      'content-type': 'application/json',
      ...(token ? { authorization: `Bearer ${token}` } : {})
    },
    body: JSON.stringify(body ?? {})
  });
  return { status: response.status, body: await response.json().catch(() => null) };
}

export async function get(base, path, token) {
  const response = await fetch(base + path, {
    headers: token ? { authorization: `Bearer ${token}` } : {}
  });
  return { status: response.status, body: await response.json().catch(() => null) };
}

let counter = 0;

export function uniqueEmail() {
  counter += 1;
  return `player${process.pid}${counter}@auralis.test`;
}

export function register(base, email = uniqueEmail(), password = 'correct horse battery') {
  return post(base, '/v1/auth/register', { email, password });
}

export class WsClient {
  constructor(url) {
    this.url = url;
    this.messages = [];
    this.waiters = [];
    this.cursor = 0;
  }

  connect() {
    return new Promise((resolve, reject) => {
      this.socket = new WebSocket(this.url);

      this.socket.onopen = () => resolve();
      this.socket.onerror = () => reject(new Error('websocket_error'));

      this.socket.onmessage = (event) => {
        let message;
        try {
          message = JSON.parse(event.data);
        } catch {
          return;
        }

        this.messages.push(message);
        for (const waiter of [...this.waiters]) {
          if (!waiter.predicate(message)) continue;
          this.waiters.splice(this.waiters.indexOf(waiter), 1);
          waiter.resolve(message);
        }
      };
    });
  }

  send(value) {
    this.socket.send(JSON.stringify(value));
  }

  next(predicate, timeoutMs = 2000) {
    for (let i = this.cursor; i < this.messages.length; i += 1) {
      if (predicate(this.messages[i])) {
        this.cursor = i + 1;
        return Promise.resolve(this.messages[i]);
      }
    }

    return new Promise((resolve, reject) => {
      const waiter = { predicate, resolve: null };

      const timer = setTimeout(() => {
        const index = this.waiters.indexOf(waiter);
        if (index >= 0) this.waiters.splice(index, 1);
        reject(new Error('websocket_timeout'));
      }, timeoutMs);

      waiter.resolve = (message) => {
        clearTimeout(timer);
        resolve(message);
      };

      this.waiters.push(waiter);
    });
  }

  close() {
    if (this.socket) this.socket.close();
  }
}
