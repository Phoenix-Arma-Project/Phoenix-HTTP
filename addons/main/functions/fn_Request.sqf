/*
    Function: PHTTP_fnc_Request

    Description:
        Builds and dispatches a single HTTP request through the extension. Because callExtension is
        synchronous and length-limited, the request is assembled across several calls: create the
        transaction, then layer on headers, query parameters and the (chunked) body, then send.
        The call returns immediately; if a callback was given the response is delivered later by the
        ExtensionCallback handler in fn_Init.

    Parameter: _request (HASHMAP) - request description with optional keys:
        "method"   (STRING)            - HTTP method, default "GET".
        "url"      (STRING)            - target URL, default "".
        "parser"   (STRING)            - "json" or "raw", how the extension shapes the response.
        "headers"  (HASHMAP)           - request headers.
        "query"    (HASHMAP)           - query-string parameters.
        "body"     (STRING or HASHMAP) - raw body, or a hashmap serialized and sent as JSON.
        "callback" (CODE)              - run with [response] when the reply arrives; omit to fire-and-forget.

    Returns:
        STRING - the transaction id, or "" if the request could not be created.
*/

params [["_request", createHashMap, [createHashMap]]];

private _method = _request getOrDefault ["method", "GET"];
private _url = _request getOrDefault ["url", ""];
private _parser = _request getOrDefault ["parser", "json"];
private _headers = _request getOrDefault ["headers", createHashMap];
private _query = _request getOrDefault ["query", createHashMap];
private _body = _request getOrDefault ["body", ""];
private _callback = _request getOrDefault ["callback", {}];

// Create the transaction. The reply is "status:data"; split on the first colon only so the data
// (here the transaction id) is preserved even if it ever contained colons of its own.
private _reply = (PHTTP_extension callExtension ["request:create", [_method, _url]]) select 0;
private _separator = _reply find ":";
private _status = _reply select [0, _separator];
private _data = _reply select [_separator + 1];

if (_status isNotEqualTo "success") exitWith {
    diag_log format ["[PhoenixHTTP] create failed: %1", _data];
    ""
};

// Layer headers and query parameters onto the transaction by id.
{
    PHTTP_extension callExtension ["request:header", [_data, _x, _y]];
} forEach _headers;

{
    PHTTP_extension callExtension ["request:query", [_data, _x, _y]];
} forEach _query;

// A hashmap body is serialized into the extension's pair format; any other body is sent as-is.
private _bodyString = if (_body isEqualType createHashMap) then {
    str ([_body] call PHTTP_fnc_Serialize)
} else {
    _body
};

// Send the body in fixed-size chunks to stay under the callExtension argument length limit.
if (_bodyString isNotEqualTo "") then {
    private _length = count _bodyString;
    private _offset = 0;
    while { _offset < _length } do {
        PHTTP_extension callExtension ["request:body", [_data, _bodyString select [_offset, PHTTP_chunkSize]]];
        _offset = _offset + PHTTP_chunkSize;
    };
};

// Register the callback (and ask for a response) only when the caller actually wants one.
private _needsResponse = if (_callback isEqualTo {}) then {
    "false"
} else {
    PHTTP_callbacks set [_data, _callback];
    "true"
};

PHTTP_extension callExtension ["request:send", [_data, _parser, _needsResponse]];

_data
