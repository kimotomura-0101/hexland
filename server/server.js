const WebSocket = require('ws');
const crypto = require('crypto');

const PORT = process.env.PORT || 8080;
const wss = new WebSocket.Server({ port: PORT });

// ===== Data Structures =====
const rooms = new Map();       // code -> Room
const socketToRoom = new Map(); // ws -> code
let nextId = 1;

function generateCode() {
  const chars = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789'; // no ambiguous chars
  let code;
  do {
    code = '';
    for (let i = 0; i < 4; i++) code += chars[Math.floor(Math.random() * chars.length)];
  } while (rooms.has(code));
  return code;
}

// ===== Connection =====
wss.on('connection', (ws) => {
  ws.id = `s${nextId++}`;
  ws.isAlive = true;
  ws.playerName = '';

  ws.on('message', (raw) => {
    let msg;
    try { msg = JSON.parse(raw); } catch { return; }
    switch (msg.type) {
      case 'create_room':   handleCreateRoom(ws, msg); break;
      case 'join_room':     handleJoinRoom(ws, msg); break;
      case 'player_ready':  handlePlayerReady(ws, msg); break;
      case 'start_game':    handleStartGame(ws); break;
      case 'game_action':   handleGameAction(ws, msg); break;
      case 'explore_sync':  handleExploreSync(ws, msg); break;
      case 'leave_room':    handleLeave(ws); break;
      case 'pong':          ws.isAlive = true; break;
    }
  });

  ws.on('close', () => handleLeave(ws));
  ws.on('error', () => {});
});

// Ping interval
setInterval(() => {
  wss.clients.forEach((ws) => {
    if (!ws.isAlive) return ws.terminate();
    ws.isAlive = false;
    send(ws, { type: 'ping' });
  });
}, 30000);

console.log(`Hexland server listening on port ${PORT}`);

// ===== Helpers =====
function send(ws, obj) {
  if (ws.readyState === WebSocket.OPEN) ws.send(JSON.stringify(obj));
}

function broadcast(room, obj, excludeWs) {
  const json = JSON.stringify(obj);
  for (const p of room.players) {
    if (p && p.ws && p.ws !== excludeWs && p.ws.readyState === WebSocket.OPEN) {
      p.ws.send(json);
    }
  }
}

function broadcastAll(room, obj) {
  broadcast(room, obj, null);
}

function sendPlayerList(room) {
  const list = room.players.map(p => p ? { name: p.name, ready: p.ready, isAI: false } : { name: 'AI', ready: true, isAI: true });
  broadcastAll(room, { type: 'player_list', players: list });
}

function findPlayerIndex(room, ws) {
  return room.players.findIndex(p => p && p.ws === ws);
}

// ===== Room Handlers =====
function handleCreateRoom(ws, msg) {
  if (socketToRoom.has(ws)) return send(ws, { type: 'error', message: 'Already in a room' });

  const code = generateCode();
  const maxPlayers = Math.min(4, Math.max(2, msg.maxPlayers || 4));
  const room = {
    code,
    host: ws,
    players: new Array(maxPlayers).fill(null),
    maxPlayers,
    mapRadius: msg.mapRadius || 2,
    state: 'lobby',
    // Game state (lightweight, for turn validation)
    currentPlayerIndex: 0,
    phase: 'Setup1',
    turnStep: 'PlaceSettlement',
  };

  room.players[0] = { ws, name: msg.name || 'Host', ready: false };
  ws.playerName = msg.name || 'Host';

  rooms.set(code, room);
  socketToRoom.set(ws, code);

  send(ws, { type: 'room_created', code, playerIndex: 0 });
  sendPlayerList(room);
}

function handleJoinRoom(ws, msg) {
  if (socketToRoom.has(ws)) return send(ws, { type: 'error', message: 'Already in a room' });

  const code = (msg.code || '').toUpperCase();
  const room = rooms.get(code);
  if (!room) return send(ws, { type: 'error', message: 'Room not found' });
  if (room.state !== 'lobby') return send(ws, { type: 'error', message: 'Game already started' });

  const slot = room.players.indexOf(null);
  if (slot === -1) return send(ws, { type: 'error', message: 'Room is full' });

  const name = msg.name || `Player${slot + 1}`;
  room.players[slot] = { ws, name, ready: false };
  ws.playerName = name;
  socketToRoom.set(ws, code);

  send(ws, { type: 'room_joined', code, playerIndex: slot });
  sendPlayerList(room);
}

function handlePlayerReady(ws, msg) {
  const code = socketToRoom.get(ws);
  if (!code) return;
  const room = rooms.get(code);
  if (!room || room.state !== 'lobby') return;

  const idx = findPlayerIndex(room, ws);
  if (idx === -1) return;
  room.players[idx].ready = !!msg.ready;
  sendPlayerList(room);
}

function handleStartGame(ws) {
  const code = socketToRoom.get(ws);
  if (!code) return;
  const room = rooms.get(code);
  if (!room || room.state !== 'lobby') return;
  if (room.host !== ws) return send(ws, { type: 'error', message: 'Only host can start' });

  // Need at least 2 human players, or host can start with AI filling
  const humanCount = room.players.filter(p => p !== null).length;
  if (humanCount < 1) return send(ws, { type: 'error', message: 'Need at least 1 player' });

  room.state = 'playing';
  room.currentPlayerIndex = 0;
  room.phase = 'Setup1';
  room.turnStep = 'PlaceSettlement';

  const seed = crypto.randomInt(1, 2147483647);
  const players = room.players.map(p => p ? { name: p.name, isAI: false } : { name: 'AI', isAI: true });

  broadcastAll(room, {
    type: 'game_start',
    seed,
    players,
    mapRadius: room.mapRadius,
  });
}

// ===== Game Action Handler =====
function handleGameAction(ws, msg) {
  const code = socketToRoom.get(ws);
  if (!code) return;
  const room = rooms.get(code);
  if (!room || room.state !== 'playing') return;

  const senderIdx = findPlayerIndex(room, ws);
  if (senderIdx === -1) return;

  const currentPlayer = room.players[room.currentPlayerIndex];
  const isCurrentPlayerAI = currentPlayer === null;
  const isHost = room.host === ws;

  // Turn validation: only current player (or host for AI) can act
  if (isCurrentPlayerAI) {
    if (!isHost) return send(ws, { type: 'action_rejected', action: msg.action, reason: 'Not your turn (AI)' });
  } else {
    if (senderIdx !== room.currentPlayerIndex) return send(ws, { type: 'action_rejected', action: msg.action, reason: 'Not your turn' });
  }

  const action = msg.action;
  const playerIndex = room.currentPlayerIndex;

  // Handle dice roll (server generates values)
  if (action === 'roll_dice') {
    const d1 = Math.floor(Math.random() * 6) + 1;
    const d2 = Math.floor(Math.random() * 6) + 1;
    broadcastAll(room, { type: 'game_action', action: 'dice_result', playerIndex, d1, d2 });

    // If 7, next step is MoveRobber
    if (d1 + d2 === 7) {
      room.turnStep = 'MoveRobber';
    } else {
      room.turnStep = 'Waiting';
    }
    return;
  }

  // Update server-side turn state based on action
  switch (action) {
    case 'build_settlement':
      if (room.phase === 'Setup1' || room.phase === 'Setup2') {
        room.turnStep = 'PlaceRoad';
      }
      break;
    case 'build_road':
      if (room.phase === 'Setup1' || room.phase === 'Setup2') {
        advanceSetupTurn(room);
      }
      break;
    case 'move_robber':
      room.turnStep = 'Waiting';
      break;
    case 'end_turn':
      advancePlayingTurn(room);
      break;
    case 'use_dev_card':
      // Handle road building card, monopoly etc.
      if (msg.cardType === 'RoadBuilding') room.turnStep = 'RoadBuildingCard';
      else if (msg.cardType === 'Monopoly') room.turnStep = 'Monopoly';
      break;
    case 'execute_monopoly':
      room.turnStep = 'Waiting';
      break;
  }

  // Relay action to all clients
  const relay = { type: 'game_action', action, playerIndex, ...msg };
  delete relay.type; // re-add properly
  broadcastAll(room, { type: 'game_action', ...relay, playerIndex });
}

function advanceSetupTurn(room) {
  if (room.phase === 'Setup1') {
    room.currentPlayerIndex++;
    if (room.currentPlayerIndex >= room.maxPlayers) {
      room.phase = 'Setup2';
      room.currentPlayerIndex = room.maxPlayers - 1;
    }
    room.turnStep = 'PlaceSettlement';
  } else if (room.phase === 'Setup2') {
    room.currentPlayerIndex--;
    if (room.currentPlayerIndex < 0) {
      room.phase = 'Playing';
      room.currentPlayerIndex = 0;
      room.turnStep = 'Waiting';
    } else {
      room.turnStep = 'PlaceSettlement';
    }
  }
}

function advancePlayingTurn(room) {
  room.currentPlayerIndex = (room.currentPlayerIndex + 1) % room.maxPlayers;
  room.turnStep = 'Waiting';
}

// ===== Explore Sync =====
function handleExploreSync(ws, msg) {
  const code = socketToRoom.get(ws);
  if (!code) return;
  const room = rooms.get(code);
  if (!room) return;

  const idx = findPlayerIndex(room, ws);
  if (idx === -1) return;

  // Relay to all others (not back to sender)
  broadcast(room, { type: 'explore_sync', playerIndex: idx, pos: msg.pos, rot: msg.rot, anim: msg.anim }, ws);
}

// ===== Leave / Disconnect =====
function handleLeave(ws) {
  const code = socketToRoom.get(ws);
  if (!code) return;
  socketToRoom.delete(ws);

  const room = rooms.get(code);
  if (!room) return;

  const idx = findPlayerIndex(room, ws);
  if (idx !== -1) {
    room.players[idx] = null; // Becomes AI slot
  }

  // If host left, assign new host
  if (room.host === ws) {
    const newHost = room.players.find(p => p !== null);
    if (newHost) {
      room.host = newHost.ws;
      send(newHost.ws, { type: 'host_changed' });
    }
  }

  // If no humans left, destroy room
  const hasHumans = room.players.some(p => p !== null);
  if (!hasHumans) {
    rooms.delete(code);
    return;
  }

  sendPlayerList(room);

  // Notify about disconnect
  broadcastAll(room, { type: 'player_disconnected', playerIndex: idx });
}
