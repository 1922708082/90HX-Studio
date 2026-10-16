@echo -off
echo ================================================================
echo   NVIDIA CMP 90HX Windows Unlock (Compute + Graphics + Gen2)
echo ================================================================

if exist NVPermissiveEFI.efi then
    NVPermissiveEFI.efi windows
endif

if exist .\NVPermissiveEFI.efi then
    .\NVPermissiveEFI.efi windows
endif

if exist \EFI\90HX\NVPermissiveEFI.efi then
    \EFI\90HX\NVPermissiveEFI.efi windows
endif

if exist fs0:\EFI\90HX\NVPermissiveEFI.efi then
    fs0:\EFI\90HX\NVPermissiveEFI.efi windows
endif

if exist fs1:\EFI\90HX\NVPermissiveEFI.efi then
    fs1:\EFI\90HX\NVPermissiveEFI.efi windows
endif

if exist fs2:\EFI\90HX\NVPermissiveEFI.efi then
    fs2:\EFI\90HX\NVPermissiveEFI.efi windows
endif

for %i in (0 1 2 3 4 5 6 7 8 9) do
    if exist fs%i:\EFI\90HX\NVPermissiveEFI.efi then
        fs%i:\EFI\90HX\NVPermissiveEFI.efi windows
    endif
endfor
