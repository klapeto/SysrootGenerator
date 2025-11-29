# SysrootGenerator

Generates debian/ubuntu based sysroots. Currently only works on linux

## Usage

Edit the `sysroot.json` file or provide a config json file from the parameter and run `SysrootGenerator`

## File config
 - arch: the target architecture of the sysroot (e.g. amd64, arm64, armhf)
 - distribution: the distribution version to use (e.g. noble, jammy)
 - path: the target directory to produce the sysroot
 - cachePath: the directory to use as path
 - packages: a list of packages to install on the sysroot
 - sources: a list of sources to get the packages
   - uri: the uri of the source
   - components: the components to use from the source (e.g. main, universe)