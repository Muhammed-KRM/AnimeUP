--[[
    AnimeUP — Otomatik Anime4K Shader Enjeksiyon Scripti
    Dosya: mpv-config/scripts/animeup-hook.lua

    Görev:
      - Video yüklendiğinde çözünürlüğü otomatik algılar.
      - Çözünürlük < 1080p ise Anime4K Mode A shader'larını etkinleştirir.
      - OSD ekranında kullanıcıya anlık durum bildirimi gösterir.
      - CTRL+0 ile shader'lar devre dışı bırakılabilir.

    Tasarım Notu:
      - Shader yolları mpv'nin ~~/shaders/ göreceli yol sözdizimi ile çözümlenir.
      - Bu script uosc ile tam uyumludur.
--]]

local mp = require 'mp'
local utils = require 'mp.utils'

-- Shader dosya adları — değiştirmek için burası düzenlenir.
local SHADER_NAMES = {
    "Anime4K_Clamp_Highlights.glsl",
    "Anime4K_Restore_CNN_VL.glsl",
    "Anime4K_Upscale_CNN_x2_VL.glsl",
    "Anime4K_AutoDownscalePre_x2.glsl",
    "Anime4K_AutoDownscalePre_x4.glsl",
    "Anime4K_Upscale_CNN_x2_M.glsl"
}

-- Etkin shader listesini oluşturur (noktalı virgülle birleştirilmiş tam yollar)
local function build_shader_string()
    local shaders_dir = mp.command_native({ "expand-path", "~~/shaders/" })
    local paths = {}
    for _, name in ipairs(SHADER_NAMES) do
        table.insert(paths, shaders_dir .. name)
    end
    return table.concat(paths, ";")
end

-- Video yüklendiğinde çözünürlük kontrolü ve otomatik shader aktivasyonu
local function on_file_loaded()
    local width  = mp.get_property_number("width", 0)
    local height = mp.get_property_number("height", 0)

    -- Ses-only dosyası veya geçersiz metadata ise işlem yapma
    if width == 0 or height == 0 then
        mp.msg.info("[AnimeUP] Video boyutu alınamadı — shader enjeksiyonu atlandı.")
        return
    end

    mp.msg.info(string.format("[AnimeUP] Video çözünürlüğü: %dx%d", width, height))

    if height < 1080 then
        -- SD/HD kaynak → Mode A shader'larını etkinleştir
        local shader_string = build_shader_string()
        mp.set_property("glsl-shaders", shader_string)
        mp.osd_message(
            string.format("AnimeUP ✦ SD→4K AI Upscale AKTİF  [%dx%d → 4K]", width, height),
            5  -- 5 saniye OSD göster
        )
        mp.msg.info("[AnimeUP] Mode A shader paketi yüklendi.")
    else
        -- 1080p+ kaynak → Shader gereksiz, kullanıcıyı bilgilendir
        mp.osd_message(
            string.format("AnimeUP ✦ Video kalitesi yeterli (%dx%d) — Shader kapalı", width, height),
            4
        )
        mp.msg.info("[AnimeUP] Kaynak çözünürlük yeterli, shader yüklenmedi.")
    end
end

-- MPV event bağlantısı
mp.register_event("file-loaded", on_file_loaded)

mp.msg.info("[AnimeUP] animeup-hook.lua başarıyla yüklendi.")
