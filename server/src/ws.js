// Minimal RFC 6455 WebSocket server: handshake, masked text frames, ping/pong and close.
// Written locally so the reference backend has zero npm dependencies.
import { createHash } from 'node:crypto';
import { EventEmitter } from 'node:events';

const GUID = '258EAFA5-E914-47DA-95CA-C5AB0DC85B11';
const MAX_MESSAGE_BYTES = 1_000_000;

export class WebSocketConnection extends EventEmitter {
  constructor(socket, request) {
    super();
    this.socket = socket;
    this.request = request;
    this.buffer = Buffer.alloc(0);
    this.closed = false;
    this.remoteAddress = request?.socket?.remoteAddress ?? 'unknown';
    this.context = null;

    socket.on('data', (chunk) => this.#onData(chunk));
    socket.on('close', () => this.#markClosed());
    socket.on('error', () => this.#markClosed());
  }

  send(text) {
    this.#sendFrame(0x1, Buffer.from(String(text), 'utf8'));
  }

  /** Feed bytes that arrived together with the HTTP upgrade request. */
  feed(chunk) {
    if (chunk && chunk.length > 0) this.#onData(chunk);
  }

  sendJson(value) {
    this.send(JSON.stringify(value));
  }

  close(code = 1000) {
    if (this.closed) return;
    const payload = Buffer.alloc(2);
    payload.writeUInt16BE(code, 0);
    this.#sendFrame(0x8, payload);
    this.#markClosed();
    this.socket.end();
  }

  #markClosed() {
    if (this.closed) return;
    this.closed = true;
    this.emit('close');
  }

  #sendFrame(opcode, payload) {
    if (this.closed || this.socket.destroyed) return;

    const length = payload.length;
    let header;

    if (length < 126) {
      header = Buffer.from([0x80 | opcode, length]);
    } else if (length < 65536) {
      header = Buffer.alloc(4);
      header[0] = 0x80 | opcode;
      header[1] = 126;
      header.writeUInt16BE(length, 2);
    } else {
      header = Buffer.alloc(10);
      header[0] = 0x80 | opcode;
      header[1] = 127;
      header.writeBigUInt64BE(BigInt(length), 2);
    }

    this.socket.write(Buffer.concat([header, payload]));
  }

  #onData(chunk) {
    this.buffer = Buffer.concat([this.buffer, chunk]);

    for (;;) {
      if (this.buffer.length < 2) return;

      const first = this.buffer[0];
      const second = this.buffer[1];
      const opcode = first & 0x0f;
      const masked = (second & 0x80) !== 0;

      let length = second & 0x7f;
      let offset = 2;

      if (length === 126) {
        if (this.buffer.length < 4) return;
        length = this.buffer.readUInt16BE(2);
        offset = 4;
      } else if (length === 127) {
        if (this.buffer.length < 10) return;
        const big = this.buffer.readBigUInt64BE(2);
        if (big > BigInt(MAX_MESSAGE_BYTES)) {
          this.close(1009);
          return;
        }
        length = Number(big);
        offset = 10;
      }

      if (length > MAX_MESSAGE_BYTES) {
        this.close(1009);
        return;
      }

      const maskOffset = offset;
      if (masked) offset += 4;
      if (this.buffer.length < offset + length) return;

      const payload = Buffer.from(this.buffer.subarray(offset, offset + length));
      if (masked) {
        for (let i = 0; i < payload.length; i += 1) {
          payload[i] ^= this.buffer[maskOffset + (i & 3)];
        }
      }
      this.buffer = this.buffer.subarray(offset + length);

      if (opcode === 0x8) {
        this.close(1000);
        return;
      }
      if (opcode === 0x9) {
        this.#sendFrame(0xA, payload);
        continue;
      }
      if (opcode === 0xA) continue;

      if (opcode === 0x1 || opcode === 0x0) {
        this.emit('message', payload.toString('utf8'));
      }
    }
  }
}

export function attachWebSocketServer(httpServer, onConnection) {
  httpServer.on('upgrade', (request, socket, head) => {
    const key = request.headers['sec-websocket-key'];
    if (!key || String(request.headers.upgrade).toLowerCase() !== 'websocket') {
      socket.destroy();
      return;
    }

    const accept = createHash('sha1').update(key + GUID).digest('base64');
    socket.write(
      'HTTP/1.1 101 Switching Protocols\r\n' +
      'Upgrade: websocket\r\n' +
      'Connection: Upgrade\r\n' +
      `Sec-WebSocket-Accept: ${accept}\r\n\r\n`
    );

    const connection = new WebSocketConnection(socket, request);
    connection.feed(head);
    onConnection(connection, request);
  });
}
