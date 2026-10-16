@echo -off
echo ============================================================
echo   CMP 90HX Unlock - auto script
echo   Step 1: run NVPermissiveEFI (graphics + compute + Gen2)
echo   Step 2: chain-load Windows Boot Manager automatically
echo ============================================================
echo
echo [1/2] Running: NVPermissiveEFI.efi windows
echo       This takes a while - please wait, do NOT power off.
echo
for %i in fs0 fs1 fs2 fs3 fs4 fs5 fs6 fs7
  if exist %i:\EFI\NVPermissive\NVPermissiveEFI.efi then
    %i:\EFI\NVPermissive\NVPermissiveEFI.efi windows
  endif
endfor
echo
echo [2/2] Unlock step finished. Chain-loading Windows Boot Manager...
for %i in fs0 fs1 fs2 fs3 fs4 fs5 fs6 fs7
  if exist %i:\EFI\Microsoft\Boot\bootmgfw.efi then
    echo       Windows found on %i: - booting now
    %i:\EFI\Microsoft\Boot\bootmgfw.efi
  endif
endfor
echo
echo [!] Windows Boot Manager not found - staying in EFI Shell.
echo     Manual unlock : NVPermissiveEFI.efi windows
echo     Then boot     : bootmgfw.efi
echo     Or type 'exit' to try the next boot entry (no unlock).
