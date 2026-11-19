using System;

namespace ProjectInfinity.Networking
{
    // Wire protocol shared with server/src/server.js. Field names must stay in sync.
    // JsonUtility needs concrete [Serializable] classes with public fields.

    [Serializable]
    public class NetPosition
    {
        public float x;
        public float y;
        public float z;
    }

    [Serializable]
    public class HelloMessage
    {
        public string type;
        public int protocol;
        public int tickHz;
    }

    [Serializable]
    public class AuthRequest
    {
        public string type = "auth";
        public string token;
    }

    [Serializable]
    public class AuthErrorMessage
    {
        public string type;
        public string reason;
    }

    [Serializable]
    public class AuthOkMessage
    {
        public string type;
        public string accountId;
        public NetCharacter character;
    }

    [Serializable]
    public class NetCharacter
    {
        public string id;
        public string name;
        public int level;
        public int xp;
        public float health;
        public float maxHealth;
        public int gold;
        public NetPosition position;
    }

    [Serializable]
    public class MoveMessage
    {
        public string type = "move";
        public float x;
        public float y;
        public float z;
        public int seq;
    }

    [Serializable]
    public class AttackMessage
    {
        public string type = "attack";
        public string targetId;
        public string abilityId = "light";
        public int seq;
    }

    [Serializable]
    public class PingMessage
    {
        public string type = "ping";
        public long t;
    }

    [Serializable]
    public class CorrectionMessage
    {
        public string type;
        public string reason;
        public NetPosition position;
        public int violations;
    }

    [Serializable]
    public class DeniedMessage
    {
        public string type;
        public string reason;
    }

    [Serializable]
    public class DamageMessage
    {
        public string type;
        public string attackerId;
        public string targetId;
        public float damage;
        public float targetHealth;
    }

    [Serializable]
    public class KillMessage
    {
        public string type;
        public string monsterId;
        public string enemyId;
        public long xp;
        public int levelsGained;
        public int level;
        public int gold;
    }

    [Serializable]
    public class NetMonster
    {
        public string id;
        public string enemyId;
        public float health;
        public NetPosition position;
    }

    [Serializable]
    public class NetPlayerState
    {
        public string id;
        public string name;
        public int level;
        public float health;
        public NetPosition position;
    }

    [Serializable]
    public class StateMessage
    {
        public string type;
        public NetPlayerState[] players;
        public NetMonster[] monsters;
    }
}
