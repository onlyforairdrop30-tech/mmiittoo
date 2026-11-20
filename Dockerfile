FROM node:22-alpine

WORKDIR /app

# Copy server files
COPY server/package.json ./
COPY server/src ./src
COPY server/test ./test

# Configure container environment
ENV HOST=0.0.0.0
ENV PORT=8787
ENV NODE_ENV=production

EXPOSE 8787

# Healthcheck on /health endpoint
HEALTHCHECK --interval=15s --timeout=3s --start-period=5s --retries=3 \
  CMD node -e "fetch('http://127.0.0.1:8787/health').then(r => r.ok ? process.exit(0) : process.exit(1)).catch(() => process.exit(1))"

CMD ["node", "src/server.js"]
