# NextBotAdapter REST API

All endpoints follow TShock's REST API conventions:

- All requests require a `token` query parameter (a valid TShock REST token)
- Read operations return flat fields directly
- Write operations return `{ "response": "..." }`
- Error responses return `{ "error": "..." }`

---

## Authentication

All endpoints require a TShock REST token with the corresponding permission. Pass the token as a query parameter:

```
GET /nextbot/users/Arispex/inventory?token=<token>
```

---

## Users

### GET `/nextbot/users/{user}/inventory`

Returns the stored inventory of the specified user.

**Permission:** `nextbot.users.inventory`

**Parameters**

| Name   | Location    | Description     |
|--------|-------------|-----------------|
| `user` | route param | Username to look up |

**Response 200**

```json
{
  "items": [
    {
      "slot": 0,
      "netId": 4,
      "stack": 1,
      "prefixId": 82
    }
  ]
}
```

| Field           | Type     | Description                        |
|-----------------|----------|------------------------------------|
| `items`         | array    | List of inventory items            |
| `items[].slot`  | integer  | Inventory slot index               |
| `items[].netId` | integer  | Item net ID (Terraria item type)   |
| `items[].stack` | integer  | Stack size                         |
| `items[].prefixId` | integer | Item prefix ID (modifier)       |

**Errors**

| Status | `error`                            | Cause                              |
|--------|------------------------------------|------------------------------------|
| 400    | `Missing required route parameter 'user'.` | `{user}` is empty          |
| 400    | `User was not found.`              | No registered account with that name |
| 400    | `Player data was not found.`       | Account exists but has no saved character data |

---

### GET `/nextbot/users/{user}/stats`

Returns the stored character stats of the specified user.

**Permission:** `nextbot.users.stats`

**Parameters**

| Name   | Location    | Description     |
|--------|-------------|-----------------|
| `user` | route param | Username to look up |

**Response 200**

```json
{
  "health": 400,
  "maxHealth": 500,
  "mana": 100,
  "maxMana": 200,
  "questsCompleted": 7,
  "deathsPve": 12,
  "deathsPvp": 3
}
```

| Field             | Type    | Description                        |
|-------------------|---------|------------------------------------|
| `health`          | integer | Current health                     |
| `maxHealth`       | integer | Maximum health                     |
| `mana`            | integer | Current mana                       |
| `maxMana`         | integer | Maximum mana                       |
| `questsCompleted` | integer | Number of Angler quests completed  |
| `deathsPve`       | integer | PvE death count                    |
| `deathsPvp`       | integer | PvP death count                    |

**Errors**

| Status | `error`                            | Cause                              |
|--------|------------------------------------|------------------------------------|
| 400    | `Missing required route parameter 'user'.` | `{user}` is empty          |
| 400    | `User was not found.`              | No registered account with that name |
| 400    | `Player data was not found.`       | Account exists but has no saved character data |

---

## World

### GET `/nextbot/world/progress`

Returns the boss and event kill status of the current world.

**Permission:** `nextbot.world.progress`

**Response 200**

```json
{
  "kingSlime": true,
  "eyeOfCthulhu": true,
  "eaterOfWorldsOrBrainOfCthulhu": false,
  "queenBee": false,
  "skeletron": true,
  "deerclops": false,
  "wallOfFlesh": true,
  "queenSlime": false,
  "theTwins": true,
  "theDestroyer": true,
  "skeletronPrime": true,
  "plantera": true,
  "golem": false,
  "dukeFishron": false,
  "empressOfLight": false,
  "lunaticCultist": false,
  "solarPillar": false,
  "nebulaPillar": false,
  "vortexPillar": false,
  "stardustPillar": false,
  "moonLord": false
}
```

All fields are `boolean`. `true` indicates the boss or event has been defeated in this world.

---

### GET `/nextbot/world/map-image`

Generates a full PNG image of the current world map and returns it as Base64. The image is generated in real time on every request.

**Permission:** `nextbot.world.map_image`

**Response 200**

```json
{
  "fileName": "map-2025-03-24_10-30-00.png",
  "base64": "<base64-encoded PNG>"
}
```

| Field      | Type   | Description                            |
|------------|--------|----------------------------------------|
| `fileName` | string | Suggested file name with timestamp     |
| `base64`   | string | Base64-encoded PNG image data          |

**Errors**

| Status | `error`              | Cause                          |
|--------|----------------------|--------------------------------|
| 500    | `<exception message>` | Map generation failed         |

---

### GET `/nextbot/world/world-file`

Reads the current world's `.wld` file and returns it as Base64.

**Permission:** `nextbot.world.world_file`

**Response 200**

```json
{
  "fileName": "MyWorld.wld",
  "base64": "<base64-encoded .wld file>"
}
```

| Field      | Type   | Description                    |
|------------|--------|--------------------------------|
| `fileName` | string | World file name                |
| `base64`   | string | Base64-encoded `.wld` file data |

**Errors**

| Status | `error`               | Cause                        |
|--------|-----------------------|------------------------------|
| 500    | `<exception message>` | World file could not be read |

---

### GET `/nextbot/world/map-file`

Generates and returns the current world's `.map` file (Terraria minimap data) as Base64. The map is fully lit before saving.

**Permission:** `nextbot.world.map_file`

**Response 200**

```json
{
  "fileName": "1.map",
  "base64": "<base64-encoded .map file>"
}
```

| Field      | Type   | Description                    |
|------------|--------|--------------------------------|
| `fileName` | string | Map file name                  |
| `base64`   | string | Base64-encoded `.map` file data |

**Errors**

| Status | `error`               | Cause                         |
|--------|-----------------------|-------------------------------|
| 500    | `<exception message>` | Map file could not be generated or read |

---

## Whitelist

### GET `/nextbot/whitelist`

Returns all users currently on the whitelist.

**Permission:** `nextbot.whitelist.view`

**Response 200**

```json
{
  "users": ["Arispex", "NextBot"]
}
```

| Field   | Type            | Description           |
|---------|-----------------|-----------------------|
| `users` | array of string | Current whitelist entries |

---

### GET `/nextbot/whitelist/add/{user}`

Adds a user to the whitelist.

**Permission:** `nextbot.whitelist.add`

**Parameters**

| Name   | Location    | Description          |
|--------|-------------|----------------------|
| `user` | route param | Username to add      |

**Response 200**

```json
{
  "response": "User 'Arispex' has been added to the whitelist."
}
```

**Errors**

| Status | `error`                               | Cause                          |
|--------|---------------------------------------|--------------------------------|
| 400    | `Whitelist user is invalid.`          | `{user}` is empty              |
| 400    | `User already exists in whitelist.`   | User is already on the whitelist |

---

### GET `/nextbot/whitelist/remove/{user}`

Removes a user from the whitelist.

**Permission:** `nextbot.whitelist.remove`

**Parameters**

| Name   | Location    | Description          |
|--------|-------------|----------------------|
| `user` | route param | Username to remove   |

**Response 200**

```json
{
  "response": "User 'Arispex' has been removed from the whitelist."
}
```

**Errors**

| Status | `error`                          | Cause                           |
|--------|----------------------------------|---------------------------------|
| 400    | `Whitelist user is invalid.`     | `{user}` is empty               |
| 400    | `User not found in whitelist.`   | User is not on the whitelist    |

---

## Config

### GET `/nextbot/config/reload`

Reloads the plugin configuration and whitelist from disk.

**Permission:** `nextbot.config.reload`

**Response 200**

```json
{
  "response": "Configuration reloaded successfully."
}
```

**Errors**

| Status | `error`               | Cause                          |
|--------|-----------------------|--------------------------------|
| 500    | `<exception message>` | Configuration reload failed    |
