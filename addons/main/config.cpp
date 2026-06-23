class CfgPatches
{
    class PhoenixHTTP
    {
        units[] = {};
        weapons[] = {};
        requiredVersion = 2.02;
        requiredAddons[] = {};
        author = "Phoenix";
        version = "0.1.0";
    };
};

class CfgFunctions
{
    class PHTTP
    {
        tag = "PHTTP";

        class core
        {
            file = "phoenixhttp\addons\main\functions";

            class Init { postInit = 1; };
            class Request {};
            class Serialize {};
        };
    };
};
