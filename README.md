# Sysroot Generator

A utility to generate a sysroot by downloading and extracting Debian/Ubuntu packages and their dependencies.
It is an alternative to `debootstrap` and `chroot` for creating a sysroot for cross-compilation without root privileges.

## Usage

The tool can be configured via command-line arguments or a JSON configuration file.

## Requirements
This program relies on existing extraction programs to extract the `.deb` files. More specifically, it requires `ar`, `gzip`, `tar` `xz` and `zstd` to be installed.

On Debian-based systems, you can install them with:
```bash
sudo apt install binutils gzip tar xz-utils zstd
```

### Command Line Arguments

| Option                         | Description                                                                                                                       |
|:-------------------------------|:----------------------------------------------------------------------------------------------------------------------------------|
| `--path`                       | **Required.** The target directory where the sysroot will be created.                                                             |
| `--distribution`               | **Required.** The distribution name (e.g., `bookworm`, `focal`, `jammy`).                                                         |
| `--packages`                   | **Required.** Comma-separated list of packages to install.                                                                        |
| `--sources`                    | **Required.** Space-separated list of sources. Format: `uri\|comp1,comp2`.                                                        |
| `--arch`                       | Target architecture (e.g., `amd64`, `arm64`, `armhf`). Default: `amd64`.                                                          |
| `--cache-path`                 | Path for downloaded packages. Default: `<path>/tmp`.                                                                              |
| `--config-file`                | Path to a JSON file containing the configuration.                                                                                 |
| `--verbose`                    | Enable verbose logging.                                                                                                           |
| `--purge`                      | Purge existing sysroot.                                                                                                           |
| `--purge-cache`                | Purge caches.                                                                                                                     |
| `--no-usr-merge`               | Do not merge `/usr` directory to root (e.g. normally `/lib` would be merged with `/usr/lib` and `/lib` will point to `/usr/lib`). |
| `--http-timeout`               | The timeout for HTTP requests in seconds. Default: `100`.                                                                         |
| `--banned-packages`            | Comma-separated list of packages to ban from installation.                                                                        |
| `--no-default-banned-packages` | Do not bypass default banned packages (e.g., linux-image, linux-headers).                                                         |
| `--help`                       | Show help information.                                                                                                            |

### Examples

#### Using Command Line Arguments
```bash
SysrootGenerator --path=./my-sysroot --distribution=jammy --packages="libc6-dev,libssl-dev,zlib1g-dev" --sources="https://archive.ubuntu.com/ubuntu/|main,universe"
```

#### Using a Configuration File
Create a `config.json`:

```json
{
  "arch": "amd64",
  "distribution":"noble",
  "path": "./x86_64-linux-gnu",
  "cachePath": "./cache",
  "packages":[
    "build-essential"
  ],
  "sources":[
    {
      "uri": "https://archive.ubuntu.com/ubuntu/",
      "components":[
        "main"
      ]
    }
  ]
}
```

Run the tool:
```bash
SysrootGenerator --config-file=config.json
```

## How it works
1.  **Metadata Download**: Downloads `Packages.gz` from the specified sources.
2.  **Dependency Resolution**: Recursively resolves all dependencies for the requested packages.
3.  **Download**: Downloads the required `.deb` files to the cache directory.
4.  **Extraction**: Extracts the data archive from the packages into the target path.
5.  **Merge usr**: Merges root directories to usr ones (e.g., `/lib` -> `usr/lib`) for compatibility.

## License
GPL-3.0