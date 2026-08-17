-- Reference frame capture for PCSX-Redux.
--
-- Dumps the PS1 display buffer on a fixed frame cadence so it can be diffed
-- pixel-for-pixel against our runtime's RECOMPONE_DUMP_DIR output. This exists
-- because every visual conclusion about Jet Moto 3 so far was reached by looking
-- at our own output with nothing to compare it against, and four were wrong.
--
-- The buffer is written raw with the geometry encoded in the filename.
-- Converting BGR555 to RGB in Lua costs 76800 iterations a frame and does not
-- keep up with the emulator; do it offline instead.
--
-- Logging goes through PCSX.log, not print: print is unavailable inside an
-- event listener, and an error thrown from one surfaces only as "Error in event
-- listener" with no detail, which is a bad way to spend a debugging session.
--
--   REF_DIR    output directory      (default "refcap")
--   REF_EVERY  dump every Nth vsync  (default 30)
--   REF_MAX    quit after N dumps    (default 600)

local dir   = os.getenv('REF_DIR')   or 'refcap'
local every = tonumber(os.getenv('REF_EVERY') or '30')
local max   = tonumber(os.getenv('REF_MAX')   or '600')

local frame, dumped = 0, 0

listener = PCSX.Events.createEventListener('GPU::Vsync', function()
    frame = frame + 1
    if frame % every ~= 0 then return end
    local ok, ss = pcall(PCSX.GPU.takeScreenShot)
    if not ok or ss == nil then return end
    -- bpp: 0 = BPP_16 (BGR555 halfwords), 1 = BPP_24 (three bytes per pixel).
    local path = string.format('%s/frame-%04d-%dx%d-b%d.bin',
                               dir, dumped, ss.width, ss.height, ss.bpp)
    local ok2, err = pcall(function()
        local f = Support.File.open(path, 'TRUNCATE')
        -- writeMoveSlice wants ownership and this slice is borrowed, so fall
        -- back to a plain write when it refuses.
        if not pcall(function() f:writeMoveSlice(ss.data) end) then f:write(ss.data) end
        f:close()
    end)
    if not ok2 then
        PCSX.log('[refcap] write failed: ' .. tostring(err) .. '\n')
        return
    end
    dumped = dumped + 1
    if dumped % 25 == 0 then
        PCSX.log(string.format('[refcap] %d dumps, %dx%d bpp=%d\n',
                               dumped, ss.width, ss.height, ss.bpp))
    end
    if dumped >= max then
        PCSX.log('[refcap] done, ' .. dumped .. ' frames\n')
        PCSX.quit(0)
    end
end)

PCSX.log('[refcap] armed: dir=' .. dir .. ' every=' .. every .. ' max=' .. max .. '\n')
