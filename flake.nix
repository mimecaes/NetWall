{
  description = "NetWall development environment";

  inputs = {
    nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";
  };

  outputs = { self, nixpkgs }:
    let
      supportedSystems = [ "x86_64-linux" "aarch64-linux" "x86_64-darwin" "aarch64-darwin" ];
      forAllSystems = nixpkgs.lib.genAttrs supportedSystems;
    in
    {
      devShells = forAllSystems (system:
        let
          pkgs = import nixpkgs { inherit system; };
          nativeLibs = with pkgs; [
            fontconfig
            freetype
            libGL
            mesa
            libx11
            libxcursor
            libxrandr
            libxi
            libxxf86vm
            libxcb
            libxkbcommon
            libICE
            libSM
            wayland
          ];
        in
        {
          default = pkgs.mkShell {
            buildInputs = [ pkgs.dotnet-sdk_10 ] ++ nativeLibs;

            shellHook = ''
              export LD_LIBRARY_PATH="${pkgs.lib.makeLibraryPath nativeLibs}:$LD_LIBRARY_PATH"
              export FONTCONFIG_PATH="${pkgs.fontconfig.out}/etc/fonts"
              echo "NetWall dev shell — .NET $(dotnet --version)"
              echo "Run: dotnet run --project src/NetWall.IDE"
            '';
          };
        });
    };
}
