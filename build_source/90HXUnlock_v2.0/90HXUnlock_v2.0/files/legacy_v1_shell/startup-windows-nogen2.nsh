@echo -off
echo ================================================================
echo   NVIDIA CMP 90HX Windows Unlock (Compute + Graphics, No Gen2)
echo ================================================================

if exist \EFI\90HX\NVPermissiveEFI.efi then
    \EFI\90HX\NVPermissiveEFI.efi windows-nogen2
endif

if exist NVPermissiveEFI.efi then
    NVPermissiveEFI.efi windows-nogen2
endif

for %i in (0 1 2 3 4 5 6 7 8 9) do
    if exist fs%i:\EFI\90HX\NVPermissiveEFI.efi then
        fs%i:\EFI\90HX\NVPermissiveEFI.efi windows-nogen2
    endif
endfor
