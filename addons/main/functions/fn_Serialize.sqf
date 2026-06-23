/*
    Function: PHTTP_fnc_Serialize

    Description:
        Recursively converts an SQF value into the pair-array format the extension decodes back into
        JSON. SQF cannot tell a hashmap apart from a plain array once written, so a nested hashmap is
        tagged as ["__MAP__", pairs] and a nil value is emitted as "__NULL__"; the extension's
        Deserializer undoes both. The top-level hashmap is returned untagged because the extension
        always treats the outermost value as a map.

    Parameter: _value (ANY) - the value to serialize (hashmap, array, scalar, string, bool, object or nil).

    Returns:
        ANY - the tagged representation ready to be stringified and sent as the request body.
*/

params ["_value"];

if (isNil "_value") exitWith { "__NULL__" };

switch (typeName _value) do {
    // Hashmap -> array of [key, value] pairs; nested maps are tagged so they round-trip as maps.
    case "HASHMAP": {
        private _pairs = [];
        {
            private _serialized = [_y] call PHTTP_fnc_Serialize;
            if (_y isEqualType createHashMap) then {
                _serialized = ["__MAP__", _serialized];
            };
            _pairs pushBack [_x, _serialized];
        } forEach _value;
        _pairs
    };
    // Array -> serialize each element, tagging any element that is itself a map.
    case "ARRAY": {
        _value apply {
            private _serialized = [_x] call PHTTP_fnc_Serialize;
            if (_x isEqualType createHashMap) then {
                _serialized = ["__MAP__", _serialized];
            };
            _serialized
        }
    };
    // Object -> "__NULL__" when null, otherwise the object reference (caller is expected to stringify).
    case "OBJECT": {
        [_value, "__NULL__"] select (isNull _value)
    };
    // Scalars, strings and booleans pass through unchanged.
    default { _value };
};
