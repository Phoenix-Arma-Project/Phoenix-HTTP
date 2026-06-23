<div align="center">

**English** | [Русский](README.ru.md)

# 🚀 Phoenix HTTP

**A lightning-fast, highly concurrent, non-blocking HTTP client for Arma 3.**

[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg)](LICENSE)

</div>

---

> [!WARNING]
> **This is a pre-release version!**
> Use in production at your own risk and only for non-critical tasks.
>
> <sub>If you find any bugs, please [open an issue](../../issues) or submit a [pull request](../../pulls) if needed.</sub>

## 📖 About

**Phoenix HTTP** is a modern HTTP client for Arma 3 dedicated servers, built as a native extension
with **C# Native AOT**. It lets your missions and frameworks talk to REST APIs and web services
without ever stalling the game loop.

It comes in two parts that work together:

* a **native extension** (`PhoenixHttp_x64.dll`) that performs the actual HTTP work asynchronously
  on a background thread pool;
* a thin **SQF wrapper** (`PHTTP_fnc_Request`) that builds requests, sends them through
  `callExtension`, and delivers responses back to your code through a callback.

Because the extension never blocks the engine and the response is parsed into a HashMap you can read
with simple dotted keys, working with web APIs from SQF stops being painful.

---

## ✨ Features

* ⚡ **Flattened JSON responses** — Arma 3 has no native JSON support, so Phoenix HTTP parses the
  response and returns a HashMap you read with dotted keys. A server payload `{"user":{"id":123}}`
  is reachable as `_response get "body.user.id"`. No SQF JSON parser required.
* 🔁 **Arrays stay iterable** — objects are flattened into dotted keys, but **arrays are kept as real
  SQF arrays** so you can `forEach` them. Each object inside an array is itself a usable HashMap.
* 🗃️ **Safe null handling** — JSON `null` values are omitted from the result, so a missing field is
  simply absent and `getOrDefault` always works. Your scripts never crash on unexpected nulls.
* ⏳ **Fully asynchronous & non-blocking** — requests run on a background thread pool and complete via
  an engine callback. The server never freezes or drops TPS while waiting for an API.
* 🧵 **Bounded concurrency** — a configurable queue limits how many requests run at once, so a burst
  of calls can never exhaust the server's resources.
* 🧩 **Macro environments** — `{{NAME}}` placeholders are replaced from `config.json`, so API keys and
  base URLs live in configuration, not hardcoded in mission files.
* 📦 **Automatic chunking** — `callExtension` has a strict reply-size limit; Phoenix HTTP splits large
  responses into chunks and reassembles them on the SQF side automatically.
* 🚀 **Zero runtime dependencies** — compiled with Native AOT, so **no .NET runtime** is needed on the
  server. Drop in the mod and go.
* 📝 **File logging** — all network activity, chunking and errors are written to per-run log files for
  easy debugging.

---

## 📋 Requirements

| Requirement | Notes |
| :--- | :--- |
| **Windows dedicated server** | The extension is built for **win-x64** only (`PhoenixHttp_x64.dll`). Linux servers are **not** supported. |
| **Arma 3 server ≥ 2.02** | Required for the array form of `callExtension`. |
| .NET runtime | **Not required** — the extension is self-contained (Native AOT). |

---

## 🛠️ Installation

1. Download the latest build from the [Releases page](../../releases).
2. Extract the `@PhoenixHTTP` folder into your Arma 3 server directory.
3. Add `-serverMod=@PhoenixHTTP` to your server startup parameters.

The `@PhoenixHTTP` folder looks like this:

```
@PhoenixHTTP/
├── addons/
│   └── phoenixhttp_main.pbo   # SQF wrapper (PHTTP_fnc_Request, callback handler)
├── PhoenixHttp_x64.dll        # native extension
├── config.json                # your configuration (copied from config.example.json)
└── logs/                      # created at runtime, one log file per server start
```

`config.json` and `logs/` live next to the DLL.

---

## ⚙️ Configuration (`config.json`)

All keys are optional; defaults are used when the file is missing or a key is absent. Numeric values
are clamped to a minimum of `1`, so an invalid value can never break the extension.

```json
{
    "maxConcurrentRequests": 8,
    "requestTimeoutSeconds": 30,
    "chunkSize": 8192,
    "debug": false,
    "environments": {
        "API_BASE": "https://api.yoursite.com/v1",
        "API_KEY": "secret_token_123"
    }
}
```

| Key | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `maxConcurrentRequests` | Integer | `8` | Maximum number of requests executed at the same time. Extra requests wait in a queue. |
| `requestTimeoutSeconds` | Integer | `30` | Per-request timeout. A request that exceeds it fails with a `Timeout` error. |
| `chunkSize` | Integer | `8192` | Maximum size, in bytes, of each response chunk handed back to SQF. |
| `debug` | Boolean | `false` | When `true`, verbose `DEBUG` lines are written to the log. |
| `environments` | Object | `{}` | Named values substituted into requests via the `{{NAME}}` macro syntax. |

The configuration can be reloaded at runtime without restarting the server — see
**Advanced: raw `callExtension` protocol** below.

---

## 📚 API Reference

The wrapper exposes a single function, `PHTTP_fnc_Request`. It takes one HashMap describing the
request and returns the transaction id (a string) immediately. The response is delivered later to
your `callback`.

```sqf
private _id = [_request] call PHTTP_fnc_Request;
```

### 📤 Request keys

| Key | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `method` | String | `"GET"` | HTTP method. Any method is allowed (`GET`, `POST`, `PUT`, `PATCH`, `DELETE`, `HEAD`, …); it is upper-cased automatically. |
| `url` | String | `""` | Target URL. Supports `{{macros}}`. |
| `parser` | String | `"json"` | `"json"` parses and flattens the response body; **any other value** (e.g. `"raw"`) returns the body as a plain string. |
| `headers` | HashMap | `createHashMap` | Request headers as name → value. Values support `{{macros}}`. |
| `query` | HashMap | `createHashMap` | Query parameters as name → value. They are URL-encoded and appended to the URL. Values support `{{macros}}`. |
| `body` | String or HashMap | `""` | Request payload. A **HashMap** is serialized to JSON and sent with `Content-Type: application/json`. A **String** is sent verbatim — set `Content-Type` yourself via `headers`. Supports `{{macros}}`. |
| `callback` | Code | `{}` | Runs when the response is ready, receiving the response HashMap as `_this select 0`. **Omit it for fire-and-forget**, where the response is discarded and never fetched. |

### 📥 Response keys

The callback receives one HashMap. Its keys are **flattened**, so you read nested values with dotted
keys rather than chained `get` calls.

| Key | Type | Description |
| :--- | :--- | :--- |
| `success` | Boolean | `true` if a response was received from the server (**any** HTTP status, including `404`/`500`). `false` only on a transport failure (network error, timeout, bad request). |
| `status_code` | Number | On success, the HTTP status code (`200`, `404`, …). On failure, a **negative error code** (see below). |
| `headers.<name>` | String | Response headers, flattened with lower-cased names — e.g. `_response get "headers.content-type"`. This is **not** a nested HashMap. |
| `body` / `body.<path>` | Any | The response payload (see **Response body: how flattening works** below). Absent when the request failed entirely. |

### ⚠️ Error codes (`status_code` when `success` is `false`)

| Code | Meaning |
| :--- | :--- |
| `-1` | Network unreachable (DNS failure, connection refused, host down). |
| `-2` | Request timed out (`requestTimeoutSeconds` exceeded). |
| `-4` | Response serialization/parsing failed (e.g. invalid JSON when `parser` is `"json"`). |
| `-100` | Unknown failure (e.g. an invalid URL or method). Check the log for details. |

### 🧬 Response body: how flattening works

The response body is normalized for SQF using one consistent rule:

* **Objects** are flattened into **dotted keys**. `{"user": {"name": "Bob"}}` becomes
  `body.user.name`, read with a single `get`.
* **Arrays** are kept as real **SQF arrays** so they stay iterable with `forEach`. Each object
  *inside* an array is itself a flattened HashMap, accessed by index then key.
* **`null`** values are dropped — the key simply does not appear.

```sqf
// Server returns: { "users": [ {"name": "Bob"}, {"name": "Alice"} ], "total": 2 }

_response get "body.total";                       // 2

private _users = _response get "body.users";      // a real SQF array
{
    diag_log (_x get "name");                     // each element is a flattened HashMap
} forEach _users;
```

When `parser` is not `"json"`, `body` is returned as a plain string instead.

---

## 💻 Examples

### 📍 GET with query parameters

```sqf
private _request = createHashMapFromArray [
    ["method", "GET"],
    ["url", "{{API_BASE}}/users/1"],
    ["query", createHashMapFromArray [
        ["include_stats", "true"]
    ]],
    ["callback", {
        params ["_response"];

        if !(_response get "success") exitWith {
            diag_log "[MyMod] Request failed (network/timeout).";
        };

        if ((_response get "status_code") != 200) exitWith {
            diag_log format ["[MyMod] API error: %1", _response get "status_code"];
        };

        // Nested fields via dotted keys; getOrDefault is safe because nulls are omitted.
        private _name = _response getOrDefault ["body.user.name", "Unknown"];
        diag_log format ["[MyMod] User: %1", _name];
    }]
];

[_request] call PHTTP_fnc_Request;
```

### 📍 POST with a JSON body

```sqf
private _request = createHashMapFromArray [
    ["method", "POST"],
    ["url", "{{API_BASE}}/users"],
    ["headers", createHashMapFromArray [
        ["Authorization", "Bearer {{API_KEY}}"]
        // Content-Type is set automatically because 'body' is a HashMap.
    ]],
    ["body", createHashMapFromArray [
        ["name", "John Doe"],
        ["role", "admin"],
        ["tags", ["pvp", "staff"]]   // nested arrays and maps are serialized too
    ]],
    ["callback", {
        params ["_response"];
        diag_log format ["[MyMod] Created -> %1", _response get "status_code"];
    }]
];

[_request] call PHTTP_fnc_Request;
```

### 📍 Raw body and custom content type

```sqf
private _request = createHashMapFromArray [
    ["method", "POST"],
    ["url", "{{API_BASE}}/webhook"],
    ["parser", "raw"],                                  // get the response as a plain string
    ["headers", createHashMapFromArray [
        ["content-type", "text/xml"]                    // set it yourself for a String body
    ]],
    ["body", "<event>player_joined</event>"],
    ["callback", {
        params ["_response"];
        diag_log (_response getOrDefault ["body", ""]);
    }]
];

[_request] call PHTTP_fnc_Request;
```

### 📍 Fire-and-forget

Omit `callback` and the response is never fetched — useful for telemetry or webhooks where you don't
care about the result.

```sqf
[createHashMapFromArray [
    ["method", "POST"],
    ["url", "{{API_BASE}}/heartbeat"]
]] call PHTTP_fnc_Request;
```

---

## 🧩 Macros

Any `{{NAME}}` token is replaced with the matching value from `environments` in `config.json`. Macros
are expanded in the **URL**, **header values**, **query values** and **body**. An unknown macro is
left untouched (braces included), so a typo is visible rather than silently blanked.

```json
"environments": { "API_BASE": "https://api.example.com", "API_KEY": "abc123" }
```
```sqf
["url", "{{API_BASE}}/status"]                       // -> https://api.example.com/status
["headers", createHashMapFromArray [["Authorization", "Bearer {{API_KEY}}"]]]
```

---

## ⚙️ Advanced: raw `callExtension` protocol

`PHTTP_fnc_Request` is built on top of these verbs. You can call them directly if you need to. Every
reply is a string in the form `status:data`, where `status` is `success` or `error` — **split on the
first colon only**, because `data` may itself contain colons.

| Command | Arguments | Returns | Description |
| :--- | :--- | :--- | :--- |
| `request:create` | `[method, url]` | `success:<id>` | Creates a transaction and returns its id. |
| `request:header` | `[id, key, value]` | `success:` | Adds a request header. |
| `request:query` | `[id, key, value]` | `success:` | Adds a query parameter. |
| `request:body` | `[id, chunk]` | `success:` | Appends a chunk to the request body. |
| `request:send` | `[id, parser, needsResponse]` | `success:<id>` | Dispatches the request. `parser` is `"json"`/`"raw"`; `needsResponse` is `"true"`/`"false"`. |
| `request:get` | `[id, chunkIndex]` | `success:<chunk>` | Returns one chunk of the ready response. |
| `request:delete` | `[id]` | `success:` | Removes a transaction. Call it after reading the response. |
| `request:clear` | `[]` | `success:` | Removes all transactions. |
| `config:reload` | `[]` | `success:` | Re-reads `config.json` at runtime (concurrency, timeout, macros, debug). |

When `needsResponse` is `"true"`, the extension raises the `ExtensionCallback` mission event handler
with `["PhoenixHttp", "response", "[""<id>"", <chunkCount>]"]` once the response is ready. You then
pull the chunks with `request:get`, concatenate them, and `call compile` the result into the response
HashMap (this is exactly what the bundled wrapper does).

```sqf
private _reply = ("PhoenixHttp" callExtension ["request:create", ["GET", "https://api.ipify.org?format=json"]]) select 0;
private _sep = _reply find ":";
private _status = _reply select [0, _sep];
private _id = _reply select [_sep + 1];     // robust: split on the first colon only

if (_status isEqualTo "success") then {
    "PhoenixHttp" callExtension ["request:send", [_id, "json", "true"]];
};
```

To reload configuration live:

```sqf
"PhoenixHttp" callExtension ["config:reload", []];
```

---

## 🔧 Building from source

You need the **.NET 10 SDK** and **[HEMTT](https://github.com/BrettMayson/HEMTT)**.

```sh
# Build the addon (PBO)
hemtt build

# Build the native extension (Windows, Native AOT)
dotnet publish extension/PhoenixHTTP/PhoenixHTTP.csproj -c Release

# Run the unit tests for the serialization/chunking logic
dotnet test extension/PhoenixHTTP.Test/PhoenixHTTP.Test.csproj
```

The extension's AOT publish requires the MSVC toolchain on Windows. CI builds the addon on Linux and
the extension on Windows, then assembles `@PhoenixHTTP`.

---

<div align="center">
Made with ❤️ for the Arma 3 community.
</div>
