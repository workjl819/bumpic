#!/usr/bin/env python3
"""将固定版本的 CleanDDD 基础配置安装到新建的 netcorepal-web 项目。"""

from __future__ import annotations

import argparse
import re
from pathlib import Path
import subprocess
import tempfile


ASSETS = Path(__file__).resolve().parent.parent / "assets"
TOKEN = "__PROJECT_NAME__"


def project_name(root: Path) -> str:
    """确认目标是单个、未配置过的 netcorepal-web 解决方案。"""
    projects = sorted((root / "src").glob("*.Web/*.Web.csproj"))
    if len(projects) != 1:
        raise RuntimeError("src 下必须恰好有一个 Web 项目")
    name = projects[0].stem.removesuffix(".Web")
    for suffix in ("Domain", "Infrastructure"):
        if not (root / f"src/{name}.{suffix}/{name}.{suffix}.csproj").is_file():
            raise RuntimeError(f"缺少 {suffix} 项目或项目名前缀不一致")
    if not (root / f"test/{name}.Web.Tests/{name}.Web.Tests.csproj").is_file():
        raise RuntimeError("缺少匹配的 Web.Tests 项目")
    return name


def ensure_directories(root: Path, name: str) -> None:
    """创建领域和应用层约定的实际目录。"""
    domain = root / "src" / f"{name}.Domain"
    web_application = root / "src" / f"{name}.Web" / "Application"
    for directory in ("AggregateModel", "DomainEvents"):
        (domain / directory).mkdir(parents=True, exist_ok=True)
    for directory in (
        "Commands",
        "DomainEventHandlers",
        "IntegrationEventConverters",
        "IntegrationEvents",
        "IntegrationEventHandlers",
        "Jobs",
        "Queries",
        "Vos",
    ):
        (web_application / directory).mkdir(parents=True, exist_ok=True)


def main() -> int:
    """先检查补丁和文件冲突，再安装配置。"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("solution_root", type=Path)
    args = parser.parse_args()
    root = args.solution_root.resolve()
    try:
        if not root.is_dir():
            raise RuntimeError(f"目录不存在：{root}")
        name = project_name(root)
        marker = root / ".codex/cleanddd-configured"
        if marker.exists():
            raise RuntimeError("该项目已经运行过基础配置脚本")

        files = []
        for source in (ASSETS / "files").rglob("*"):
            if not source.is_file():
                continue
            relative = Path(str(source.relative_to(ASSETS / "files")).replace(TOKEN, name))
            destination = root / relative
            content = source.read_bytes().replace(TOKEN.encode(), name.encode())
            if destination.exists() and destination.read_bytes() != content:
                raise RuntimeError(f"目标文件已存在且内容不同：{relative}")
            files.append((destination, content))

        redis_extension = root / f"src/{name}.Web/Extensions/StackExchangeRedisDataProtectionBuilderExtensions.cs"
        if not redis_extension.exists():
            content = (ASSETS / "optional/StackExchangeRedisDataProtectionBuilderExtensions.cs").read_bytes()
            files.append((redis_extension, content.replace(TOKEN.encode(), name.encode())))

        patch_text = (ASSETS / "configuration.patch").read_text().replace(TOKEN, name)
        optional_paths = {
            f"src/{name}.Web/Extensions/StackExchangeRedisDataProtectionBuilderExtensions.cs",
            f"src/{name}.Web/Extensions/SwaggerGenOptionsExtionsions.cs",
        }
        sections = re.split(r"(?=^--- a/)", patch_text, flags=re.MULTILINE)
        patch_text = "".join(
            section for section in sections
            if not section.startswith("--- a/")
            or section.splitlines()[0][6:] not in optional_paths
            or (root / section.splitlines()[0][6:]).exists()
        )
        with tempfile.NamedTemporaryFile(mode="w", encoding="utf-8", suffix=".patch") as patch_file:
            patch_file.write(patch_text)
            patch_file.flush()
            command = ["patch", "-d", str(root), "-p1", "--batch", "--forward", "-i", patch_file.name]
            checked = subprocess.run(command + ["--dry-run"], capture_output=True, text=True)
            if checked.returncode:
                raise RuntimeError("目标模板与配置补丁不匹配：\n" + checked.stdout + checked.stderr)
            applied = subprocess.run(command, capture_output=True, text=True)
            if applied.returncode:
                raise RuntimeError("应用补丁失败：\n" + applied.stdout + applied.stderr)

        for destination, content in files:
            destination.parent.mkdir(parents=True, exist_ok=True)
            destination.write_bytes(content)
        ensure_directories(root, name)
        marker.parent.mkdir(parents=True, exist_ok=True)
        marker.write_text("CleanDDD 基础配置已安装。\n", encoding="utf-8")
        print(f"已配置 {name}：应用基础配置补丁并安装 {len(files)} 个文件。")
        return 0
    except RuntimeError as error:
        parser.exit(1, f"错误：{error}\n")


if __name__ == "__main__":
    raise SystemExit(main())
