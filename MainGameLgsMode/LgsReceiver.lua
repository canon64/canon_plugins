-- Installed only in the KKS LGS profile. Settings are deliberately separate from G-key assignments.
-- The installer supplies the absolute commandPath; this file is a reusable receiver factory.
function CreateKksMKeyReceiver(commandPath, options)
    options = options or {}
    local family = options.family or "lhc"
    local delay = options.pollMilliseconds or 150
    local logging = options.enableLogs == true
    local lastToken = nil

    return function(event)
        if event ~= "PROFILE_ACTIVATED" then return end
        if type(dofile) ~= "function" then
            if logging then OutputLogMessage("KKS bridge: dofile unavailable\n") end
            return
        end
        -- LGS runs scripts on its own thread; normal assigned G-key macros remain in the profiler.
        -- On profile deactivation LGS aborts this thread (documented timeout: one second).
        while true do
            local ok, command = pcall(dofile, commandPath)
            if ok and type(command) == "table" and command.version == 1
                and type(command.token) == "string" and command.token ~= lastToken then
                lastToken = command.token
                local mode = command.mode
                local now = GetDate("%Y%m%d%H%M%S")
                if (mode == 1 or mode == 2 or mode == 3)
                    and type(command.expires) == "string" and now <= command.expires then
                    SetMKeyState(mode, family)
                    if logging then OutputLogMessage("KKS bridge requested M%d\n", mode) end
                end
            end
            Sleep(delay)
        end
    end
end
