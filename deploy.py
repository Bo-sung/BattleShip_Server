#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
BattleShip Server 배포 스크립트
Requirements: pip install paramiko
"""

import os
import sys
import subprocess
import getpass
import shutil
import tempfile
from pathlib import Path

try:
    import paramiko
except ImportError:
    print("[오류] paramiko 라이브러리가 필요합니다.")
    print("       pip install paramiko")
    sys.exit(1)

# ── 상수 ────────────────────────────────────────────────────────────

SCRIPT_DIR = Path(__file__).parent.resolve()

SERVICES = {
    "1": ("AuthServer",  "BattleShip.AuthServer"),
    "2": ("LobbyServer", "BattleShip.LobbyServer"),
    "3": ("GameSession", "BattleShip.GameSession"),
}

OS_OPTIONS = {
    "1": ("Windows",                "win-x64"),
    "2": ("Linux   (x64)",          "linux-x64"),
    "3": ("Linux   (ARM64)",        "linux-arm64"),
    "4": ("macOS   (Intel x64)",    "osx-x64"),
    "5": ("macOS   (Apple Silicon)", "osx-arm64"),
}

DEFAULT_DEPLOY_PATHS = {
    "win-x64":    r"C:\battleship",
    "linux-x64":  "~/battleship",
    "linux-arm64":"~/battleship",
    "osx-x64":    "~/battleship",
    "osx-arm64":  "~/battleship",
}

# ── 입력 헬퍼 ────────────────────────────────────────────────────────

def prompt(label: str, default: str = None, secret: bool = False) -> str:
    display = f"{label} [{default}]: " if default else f"{label}: "
    value = (getpass.getpass(display) if secret else input(display)).strip()
    if not value and default is not None:
        return default
    return value


def select_multiple(options: dict, title: str) -> list:
    print(f"\n{title}")
    for key, val in options.items():
        print(f"  {key}. {val[0]}")
    print("  a. 전체 선택")
    raw = input("선택 (쉼표 구분, 예: 1,2): ").strip().lower()

    if raw == "a":
        return list(options.keys())

    selected = []
    for c in raw.split(","):
        c = c.strip()
        if c in options:
            selected.append(c)
        else:
            print(f"  [경고] 잘못된 선택 무시됨: {c}")
    return selected


def select_one(options: dict, title: str) -> str:
    print(f"\n{title}")
    for key, val in options.items():
        print(f"  {key}. {val[0]}")
    while True:
        choice = input("선택: ").strip()
        if choice in options:
            return choice
        print("  올바른 번호를 입력하세요.")


# ── 빌드 ─────────────────────────────────────────────────────────────

def build_service(project_name: str, runtime: str, output_dir: Path) -> bool:
    project_path = SCRIPT_DIR / project_name
    print(f"\n  [빌드] {project_name} ({runtime}) ...")

    cmd = [
        "dotnet", "publish", str(project_path),
        "-c", "Release",
        "-r", runtime,
        "--self-contained", "true",
        "-p:PublishSingleFile=true",
        "-o", str(output_dir),
        "--nologo",
        "-v", "quiet",
    ]

    result = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8", errors="replace")
    if result.returncode != 0:
        print(f"  [실패]\n{result.stderr.strip()}")
        return False

    print(f"  [완료] → {output_dir}")
    return True


# ── SSH/SFTP ──────────────────────────────────────────────────────────

def create_ssh(host: str, port: int, username: str, password: str) -> paramiko.SSHClient:
    client = paramiko.SSHClient()
    client.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    print(f"\n[SSH] {username}@{host}:{port} 연결 중...")
    client.connect(host, port=port, username=username, password=password, timeout=15)
    print("  [완료] 연결 성공")
    return client


def run_remote(ssh: paramiko.SSHClient, cmd: str, ignore_error: bool = False) -> tuple:
    _, stdout, stderr = ssh.exec_command(cmd)
    code = stdout.channel.recv_exit_status()
    out  = stdout.read().decode(errors="replace").strip()
    err  = stderr.read().decode(errors="replace").strip()
    if out:
        print(f"    > {out}")
    if err and not ignore_error:
        print(f"    [err] {err}")
    return code, out, err


def upload_directory(sftp: paramiko.SFTPClient, local_dir: Path, remote_dir: str,
                     _top_level: bool = True):
    """디렉토리 전체를 원격 서버에 업로드합니다.
    최상위 디렉토리는 SSH mkdir -p로 이미 생성되어 있어야 합니다.
    하위 디렉토리는 SFTP로 직접 생성합니다.
    """
    remote_dir = remote_dir.replace("\\", "/")

    if not _top_level:
        try:
            sftp.mkdir(remote_dir)
        except OSError:
            pass  # 이미 존재

    items = list(local_dir.iterdir())
    for i, item in enumerate(items, 1):
        remote_path = f"{remote_dir}/{item.name}"
        if item.is_file():
            sys.stdout.write(f"\r    파일 업로드: {i}/{len(items)} — {item.name[:40]:<40}")
            sys.stdout.flush()
            sftp.put(str(item), remote_path)
        elif item.is_dir():
            upload_directory(sftp, item, remote_path, _top_level=False)
    print()  # 줄바꿈


# ── 의존성 체크 & 설치 ───────────────────────────────────────────────

def run_sudo(ssh: paramiko.SSHClient, cmd: str, password: str) -> tuple:
    """sudo -S 로 stdin에 패스워드를 전달하여 명령을 실행합니다."""
    stdin, stdout, stderr = ssh.exec_command(f"sudo -S {cmd}")
    stdin.write(password + "\n")
    stdin.flush()

    exit_code = stdout.channel.recv_exit_status()
    out = stdout.read().decode(errors="replace").strip()
    err = stderr.read().decode(errors="replace").strip()

    if out:
        for line in out.splitlines():
            if line.strip():
                print(f"    > {line.strip()}")

    # sudo 패스워드 프롬프트 줄은 제외하고 실제 오류만 출력
    for line in err.splitlines():
        line = line.strip()
        if line and not any(x in line.lower() for x in ["password", "[sudo]", "passwd"]):
            print(f"    [err] {line}")

    return exit_code, out


def detect_pkg_manager(ssh: paramiko.SSHClient) -> str:
    """패키지 매니저를 감지합니다. 반환값: apt-get | yum | brew | unknown"""
    # apt-get, yum은 일반 PATH에 있음
    for mgr in ("apt-get", "yum"):
        code, _, _ = run_remote(ssh, f"which {mgr}", ignore_error=True)
        if code == 0:
            return mgr

    # Homebrew: Intel Mac은 /usr/local/bin, Apple Silicon은 /opt/homebrew/bin
    brew_paths = (
        "/opt/homebrew/bin/brew",   # Apple Silicon
        "/usr/local/bin/brew",      # Intel Mac
    )
    for brew_path in brew_paths:
        code, _, _ = run_remote(ssh, f"test -x {brew_path}", ignore_error=True)
        if code == 0:
            # PATH에 추가되도록 심볼릭 링크 확인
            run_remote(ssh, f"export PATH=$(dirname {brew_path}):$PATH", ignore_error=True)
            return "brew"

    return "unknown"


def is_installed(ssh: paramiko.SSHClient, binary: str,
                 pkg_manager: str = None, package: str = None) -> bool:
    """설치 여부 확인. brew는 brew list로 확인 (SSH PATH 영향 없음)."""
    if pkg_manager == "brew" and package:
        brew = find_brew_path(ssh)
        code, _, _ = run_remote(ssh, f"{brew} list {package}", ignore_error=True)
        return code == 0
    code, _, _ = run_remote(ssh, f"which {binary}", ignore_error=True)
    return code == 0


def fix_brew_permissions(ssh: paramiko.SSHClient, password: str):
    """Homebrew 디렉토리 소유권을 현재 유저로 수정합니다."""
    brew = find_brew_path(ssh)
    _, prefix, _ = run_remote(ssh, f"{brew} --prefix", ignore_error=True)
    if not prefix:
        return
    print(f"    Homebrew 권한 수정 중 ({prefix}) ...")
    run_sudo(ssh, f"chown -R $(whoami) {prefix}", password)


def find_brew_path(ssh: paramiko.SSHClient) -> str:
    """Homebrew 실행 경로를 반환합니다."""
    for path in ("/opt/homebrew/bin/brew", "/usr/local/bin/brew"):
        code, _, _ = run_remote(ssh, f"test -x {path}", ignore_error=True)
        if code == 0:
            return path
    return "brew"


def install_package(ssh: paramiko.SSHClient, pkg_manager: str,
                    package: str, password: str) -> bool:
    print(f"    설치 중: {package} ...")
    if pkg_manager == "apt-get":
        is_mysql = package.startswith("mysql-server")
        if is_mysql:
            # MySQL 공식 APT 저장소 추가 후 버전 고정 설치
            print(f"    MySQL {MYSQL_VERSION} 공식 저장소 추가 중...")
            run_sudo(ssh,
                "apt-get install -y wget lsb-release gnupg && "
                "wget -q https://dev.mysql.com/get/mysql-apt-config_0.8.29-1_all.deb -O /tmp/mysql-apt-config.deb && "
                "DEBIAN_FRONTEND=noninteractive dpkg -i /tmp/mysql-apt-config.deb && "
                "apt-get update -qq",
                password)
        code, _ = run_sudo(ssh, f"DEBIAN_FRONTEND=noninteractive apt-get install -y {package}", password)
    elif pkg_manager == "yum":
        is_mysql = "mysql" in package
        if is_mysql:
            # MySQL 공식 YUM 저장소 추가
            print(f"    MySQL {MYSQL_VERSION} 공식 저장소 추가 중...")
            run_sudo(ssh,
                "yum install -y https://dev.mysql.com/get/mysql80-community-release-el7-11.noarch.rpm && "
                "yum-config-manager --enable mysql80-community",
                password)
        code, _ = run_sudo(ssh, f"yum install -y {package}", password)
    elif pkg_manager == "brew":
        brew = find_brew_path(ssh)
        code, _, _ = run_remote(ssh, f"{brew} install {package}")
        if code == 0 and package.startswith("mysql@"):
            # 버전 고정 mysql@8.0은 link 명령 필요
            run_remote(ssh, f"{brew} link {package} --force --overwrite", ignore_error=True)
    else:
        print(f"    [오류] 지원하지 않는 패키지 매니저: {pkg_manager}")
        return False
    return code == 0


# 패키지 매니저별 패키지명 매핑
MYSQL_VERSION   = "8.0.40"
MYSQL_MAJOR     = "8.0"

REDIS_PACKAGES  = {"apt-get": "redis-server",              "yum": "redis",               "brew": "redis"}
MYSQL_PACKAGES  = {"apt-get": f"mysql-server={MYSQL_VERSION}*", "yum": f"mysql-community-server-{MYSQL_VERSION}", "brew": f"mysql@{MYSQL_MAJOR}"}
REDIS_BINARIES  = {"apt-get": "redis-server",              "yum": "redis-server",        "brew": "redis-server"}
MYSQL_BINARIES  = {"apt-get": "mysql",                     "yum": "mysql",               "brew": "mysql"}

# 서비스 시작 명령
REDIS_START = {
    "apt-get": "systemctl enable redis-server && systemctl start redis-server",
    "yum":     "systemctl enable redis        && systemctl start redis",
    "brew":    "brew services start redis",
}
MYSQL_START = {
    "apt-get": "systemctl enable mysql        && systemctl start mysql",
    "yum":     "systemctl enable mysqld       && systemctl start mysqld",
    "brew":    f"brew services start mysql@{MYSQL_MAJOR}",
}


def check_and_install_deps(ssh: paramiko.SSHClient, runtime: str, password: str):
    """Redis, MySQL 설치 여부 확인 및 미설치 시 설치를 제안합니다."""
    if runtime == "win-x64":
        print("\n[의존성] Windows 서버는 수동 설치가 필요합니다. 건너뜁니다.")
        return

    if runtime == "osx-arm64":
        print("\n[의존성] Apple Silicon — Homebrew(arm64) 기반으로 체크합니다.")
    elif runtime == "osx-x64":
        print("\n[의존성] macOS Intel — Homebrew(x86_64) 기반으로 체크합니다.")

    print("\n[의존성 확인] Redis / MySQL 체크 중...")

    pkg_mgr = detect_pkg_manager(ssh)
    if pkg_mgr == "unknown":
        print("  [경고] 패키지 매니저를 찾을 수 없어 의존성 확인을 건너뜁니다.")
        return

    print(f"  패키지 매니저: {pkg_mgr}")

    redis_bin = REDIS_BINARIES.get(pkg_mgr, "redis-server")
    mysql_bin = MYSQL_BINARIES.get(pkg_mgr, "mysql")

    redis_ok = is_installed(ssh, redis_bin, pkg_manager=pkg_mgr, package=REDIS_PACKAGES.get(pkg_mgr))
    mysql_ok = is_installed(ssh, mysql_bin, pkg_manager=pkg_mgr, package=MYSQL_PACKAGES.get(pkg_mgr))

    print(f"  Redis : {'✔ 설치됨' if redis_ok else '✘ 미설치'}")
    print(f"  MySQL : {'✔ 설치됨' if mysql_ok else '✘ 미설치'}")

    missing = []
    if not redis_ok:
        missing.append(("Redis", REDIS_PACKAGES.get(pkg_mgr, "redis"), REDIS_START.get(pkg_mgr)))
    if not mysql_ok:
        missing.append(("MySQL", MYSQL_PACKAGES.get(pkg_mgr, "mysql"), MYSQL_START.get(pkg_mgr)))

    if not missing:
        print("  모두 설치되어 있습니다.")
        return

    names = ", ".join(m[0] for m in missing)
    answer = input(f"\n  {names} 이(가) 설치되어 있지 않습니다. 지금 설치하시겠습니까? (y/N): ").strip().lower()
    if answer != "y":
        print("  설치를 건너뜁니다. 서비스 실행 시 오류가 발생할 수 있습니다.")
        return

    # brew: 설치 전 디렉토리 소유권 확인 및 수정
    if pkg_mgr == "brew":
        fix_brew_permissions(ssh, password)

    for name, pkg, start_cmd in missing:
        print(f"\n  [{name}] 설치 시작...")
        if pkg_mgr == "apt-get":
            run_sudo(ssh, "apt-get update -qq", password)

        ok = install_package(ssh, pkg_mgr, pkg, password)
        if not ok:
            print(f"  [{name}] 설치 실패. 수동으로 설치해주세요.")
            continue

        print(f"  [{name}] 서비스 시작...")
        if start_cmd:
            if pkg_mgr == "brew":
                brew = find_brew_path(ssh)
                run_remote(ssh, start_cmd.replace("brew ", f"{brew} "), ignore_error=True)
            else:
                run_sudo(ssh, start_cmd.replace("sudo -S ", ""), password)

        print(f"  [{name}] 완료")


# ── OS별 배포 (서비스 등록) ───────────────────────────────────────────

def deploy_linux(ssh, sftp, service_name: str, project_name: str,
                 local_dir: Path, remote_base: str,
                 username: str, password: str):
    remote_dir = f"{remote_base}/{service_name.lower()}"
    exe        = project_name
    svc_label  = f"battleship-{service_name.lower()}"

    print(f"\n[배포] {service_name} → {remote_dir}")

    # 기존 서비스 중지
    run_remote(ssh, f"systemctl stop {svc_label} 2>/dev/null || true", ignore_error=True)

    # 파일 업로드
    run_remote(ssh, f"mkdir -p {remote_dir}")
    upload_directory(sftp, local_dir, remote_dir)
    run_remote(ssh, f"chmod +x {remote_dir}/{exe}")

    # systemd unit 파일 생성 후 /tmp 경유로 이동
    unit = (
        f"[Unit]\n"
        f"Description=BattleShip {service_name}\n"
        f"After=network.target\n\n"
        f"[Service]\n"
        f"Type=simple\n"
        f"User={username}\n"
        f"WorkingDirectory={remote_dir}\n"
        f"ExecStart={remote_dir}/{exe}\n"
        f"Restart=on-failure\n"
        f"RestartSec=5\n"
        f"StandardOutput=journal\n"
        f"StandardError=journal\n\n"
        f"[Install]\n"
        f"WantedBy=multi-user.target\n"
    )
    tmp_unit = f"/tmp/{svc_label}.service"
    with sftp.open(tmp_unit, "w") as f:
        f.write(unit)

    run_sudo(ssh,
        f"mv {tmp_unit} /etc/systemd/system/{svc_label}.service && "
        f"systemctl daemon-reload && "
        f"systemctl enable {svc_label} && "
        f"systemctl restart {svc_label}",
        password)

    print(f"  [완료] systemd 서비스 등록 완료")
    print(f"  상태 확인: systemctl status {svc_label}")


def deploy_macos(ssh, sftp, service_name: str, project_name: str,
                 local_dir: Path, remote_base: str,
                 username: str, password: str):
    remote_dir  = f"{remote_base}/{service_name.lower()}"
    exe         = project_name
    svc_label   = f"com.battleship.{service_name.lower()}"
    agents_dir  = f"/Users/{username}/Library/LaunchAgents"
    plist_path  = f"{agents_dir}/{svc_label}.plist"

    print(f"\n[배포] {service_name} → {remote_dir}")

    # 기존 에이전트 언로드
    run_remote(ssh, f"launchctl unload {plist_path} 2>/dev/null || true", ignore_error=True)

    # 파일 업로드
    run_remote(ssh, f"mkdir -p {remote_dir}")
    upload_directory(sftp, local_dir, remote_dir)
    run_remote(ssh, f"chmod +x {remote_dir}/{exe}")

    # LaunchAgents 디렉토리 생성 및 plist 작성
    run_remote(ssh, f"mkdir -p {agents_dir}")
    plist = (
        '<?xml version="1.0" encoding="UTF-8"?>\n'
        '<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN"'
        ' "http://www.apple.com/DTDs/PropertyList-1.0.dtd">\n'
        '<plist version="1.0">\n'
        '<dict>\n'
        f'    <key>Label</key>\n'
        f'    <string>{svc_label}</string>\n'
        f'    <key>ProgramArguments</key>\n'
        f'    <array>\n'
        f'        <string>{remote_dir}/{exe}</string>\n'
        f'    </array>\n'
        f'    <key>WorkingDirectory</key>\n'
        f'    <string>{remote_dir}</string>\n'
        f'    <key>RunAtLoad</key>\n'
        f'    <true/>\n'
        f'    <key>KeepAlive</key>\n'
        f'    <true/>\n'
        f'    <key>StandardOutPath</key>\n'
        f'    <string>{remote_dir}/stdout.log</string>\n'
        f'    <key>StandardErrorPath</key>\n'
        f'    <string>{remote_dir}/stderr.log</string>\n'
        '</dict>\n'
        '</plist>\n'
    )
    with sftp.open(plist_path, "w") as f:
        f.write(plist)

    run_remote(ssh, f"launchctl load {plist_path}")

    print(f"  [완료] launchd 에이전트 등록 완료")
    print(f"  상태 확인: launchctl list | grep battleship")


def deploy_windows(ssh, sftp, service_name: str, project_name: str,
                   local_dir: Path, remote_base: str,
                   username: str, password: str):
    remote_dir_sftp = f"{remote_base}/{service_name.lower()}".replace("\\", "/")
    remote_dir_win  = f"{remote_base}\\{service_name.lower()}"
    exe      = f"{project_name}.exe"
    svc_name = f"BattleShip{service_name}"

    print(f"\n[배포] {service_name} → {remote_dir_win}")

    # 기존 서비스 중지 및 삭제
    run_remote(ssh, f"sc stop {svc_name}",   ignore_error=True)
    run_remote(ssh, f"sc delete {svc_name}", ignore_error=True)

    # 파일 업로드
    upload_directory(sftp, local_dir, remote_dir_sftp)

    # Windows Service 등록 및 시작
    run_remote(ssh,
        f'sc create {svc_name} '
        f'binPath= "{remote_dir_win}\\{exe}" '
        f'start= auto '
        f'DisplayName= "BattleShip {service_name}"')
    run_remote(ssh, f'sc description {svc_name} "BattleShip {service_name} Server"')
    run_remote(ssh, f"sc start {svc_name}")

    print(f"  [완료] Windows 서비스 등록 완료")
    print(f"  상태 확인: sc query {svc_name}")


DEPLOYERS = {
    "linux-x64":  deploy_linux,
    "linux-arm64": deploy_linux,
    "osx-x64":    deploy_macos,
    "osx-arm64":  deploy_macos,
    "win-x64":    deploy_windows,
}

# ── 방화벽 설정 ───────────────────────────────────────────────────────

# 외부 허용 포트
FW_PUBLIC_PORTS  = [7001, 7002]
FW_PUBLIC_RANGE  = (7010, 7200)   # GameSession 범위
# 내부 전용 포트 (localhost만 허용)
FW_INTERNAL_PORT = 8002


def _fw_linux(ssh, password):
    """Linux 방화벽 설정 (ufw → firewalld → iptables 순으로 시도)"""
    # ── ufw ──────────────────────────────────────────────────
    code, _, _ = run_remote(ssh, "which ufw", ignore_error=True)
    if code == 0:
        print("  방화벽: ufw")
        cmds = []
        for port in FW_PUBLIC_PORTS:
            cmds.append(f"ufw allow {port}/tcp")
        cmds.append(f"ufw allow {FW_PUBLIC_RANGE[0]}:{FW_PUBLIC_RANGE[1]}/tcp")
        # 8002는 localhost만 허용
        cmds.append(f"ufw deny {FW_INTERNAL_PORT}/tcp")
        cmds.append(f"ufw allow from 127.0.0.1 to any port {FW_INTERNAL_PORT} proto tcp")
        cmds.append("ufw --force enable")
        for c in cmds:
            run_sudo(ssh, c, password)
        return

    # ── firewalld ─────────────────────────────────────────────
    code, _, _ = run_remote(ssh, "which firewall-cmd", ignore_error=True)
    if code == 0:
        print("  방화벽: firewalld")
        cmds = []
        for port in FW_PUBLIC_PORTS:
            cmds.append(f"firewall-cmd --permanent --add-port={port}/tcp")
        cmds.append(f"firewall-cmd --permanent --add-port={FW_PUBLIC_RANGE[0]}-{FW_PUBLIC_RANGE[1]}/tcp")
        cmds.append(
            f"firewall-cmd --permanent --add-rich-rule="
            f"'rule family=ipv4 source address=127.0.0.1 "
            f"port port={FW_INTERNAL_PORT} protocol=tcp accept'"
        )
        cmds.append(f"firewall-cmd --permanent --remove-port={FW_INTERNAL_PORT}/tcp 2>/dev/null || true")
        cmds.append("firewall-cmd --reload")
        for c in cmds:
            run_sudo(ssh, c, password)
        return

    # ── iptables ──────────────────────────────────────────────
    print("  방화벽: iptables")
    cmds = []
    for port in FW_PUBLIC_PORTS:
        cmds.append(f"iptables -A INPUT -p tcp --dport {port} -j ACCEPT")
    cmds.append(
        f"iptables -A INPUT -p tcp --dport {FW_PUBLIC_RANGE[0]}:{FW_PUBLIC_RANGE[1]} -j ACCEPT"
    )
    cmds.append(f"iptables -A INPUT -p tcp --dport {FW_INTERNAL_PORT} -s 127.0.0.1 -j ACCEPT")
    cmds.append(f"iptables -A INPUT -p tcp --dport {FW_INTERNAL_PORT} -j DROP")
    cmds.append("iptables-save > /etc/iptables/rules.v4 2>/dev/null || true")
    for c in cmds:
        run_sudo(ssh, c, password)


def _fw_macos(ssh, password):
    """macOS 방화벽 설정 (pf)"""
    print("  방화벽: pf")

    public_ports = ", ".join(str(p) for p in FW_PUBLIC_PORTS)
    anchor_rules = (
        f"# BattleShip rules\n"
        f"pass in proto tcp from any to any port {{{public_ports}}} keep state\n"
        f"pass in proto tcp from any to any port {{{FW_PUBLIC_RANGE[0]}:<{FW_PUBLIC_RANGE[1]}}} keep state\n"
        f"pass in proto tcp from 127.0.0.1 to any port {FW_INTERNAL_PORT} keep state\n"
        f"block in proto tcp from any to any port {FW_INTERNAL_PORT}\n"
    )

    # anchor 파일 작성 후 pf.conf에 포함
    anchor_file = "/etc/pf.anchors/battleship"
    run_sudo(ssh, f"bash -c \"cat > {anchor_file} << 'PFEOF'\n{anchor_rules}PFEOF\"", password)
    # pf.conf에 anchor 추가 (중복 방지)
    run_sudo(ssh,
        f"grep -q 'battleship' /etc/pf.conf || "
        f"echo 'anchor \"battleship\"\\nload anchor \"battleship\" from \"{anchor_file}\"' "
        f">> /etc/pf.conf",
        password)
    run_sudo(ssh, "pfctl -f /etc/pf.conf && pfctl -e", password)


def _fw_windows(ssh):
    """Windows Defender 방화벽 설정"""
    print("  방화벽: Windows Defender")

    # 기존 규칙 삭제 후 재생성
    run_remote(ssh, 'netsh advfirewall firewall delete rule name="BattleShip" 2>nul', ignore_error=True)

    port_list = ",".join(str(p) for p in FW_PUBLIC_PORTS)
    game_range = f"{FW_PUBLIC_RANGE[0]}-{FW_PUBLIC_RANGE[1]}"

    run_remote(ssh,
        f'netsh advfirewall firewall add rule name="BattleShip Public" '
        f'dir=in action=allow protocol=TCP localport="{port_list},{game_range}"')
    # 8002 localhost만 허용 (Windows는 remoteip로 제한)
    run_remote(ssh,
        f'netsh advfirewall firewall add rule name="BattleShip Internal" '
        f'dir=in action=allow protocol=TCP localport={FW_INTERNAL_PORT} remoteip=127.0.0.1')
    run_remote(ssh,
        f'netsh advfirewall firewall add rule name="BattleShip Internal Block" '
        f'dir=in action=block protocol=TCP localport={FW_INTERNAL_PORT}')


def configure_firewall(ssh, runtime: str, password: str):
    print("\n[방화벽 설정]")
    print(f"  외부 허용 : {FW_PUBLIC_PORTS} + {FW_PUBLIC_RANGE[0]}~{FW_PUBLIC_RANGE[1]}")
    print(f"  내부 전용  : {FW_INTERNAL_PORT} (localhost만)")

    if runtime in ("linux-x64", "linux-arm64"):
        _fw_linux(ssh, password)
    elif runtime in ("osx-x64", "osx-arm64"):
        _fw_macos(ssh, password)
    elif runtime == "win-x64":
        _fw_windows(ssh)

    print("  [완료] 방화벽 설정 완료")


# ── 메인 ─────────────────────────────────────────────────────────────

def main():
    print("=" * 58)
    print("   BattleShip Server 배포 스크립트")
    print("=" * 58)

    # ── 1~4. 연결 정보 ──────────────────────────────────────────
    print("\n── 연결 정보 ─────────────────────────────────────────")
    host     = prompt("서버 주소")
    ssh_port = int(prompt("SSH 포트", default="22"))
    username = prompt("유저명")
    password = prompt("비밀번호", secret=True)

    # ── 5. 배포 범위 ──────────────────────────────────────────
    selected = select_multiple(SERVICES, "── 배포 범위 ─────────────────────────────────────────")
    if not selected:
        print("[오류] 배포할 서비스를 선택하세요.")
        sys.exit(1)

    # ── 6. 대상 OS ────────────────────────────────────────────
    os_key          = select_one(OS_OPTIONS, "── 대상 OS ───────────────────────────────────────────")
    os_name, runtime = OS_OPTIONS[os_key]

    # 배포 경로 (수정 가능)
    default_path = DEFAULT_DEPLOY_PATHS[runtime]
    remote_base  = prompt(f"\n원격 배포 경로", default=default_path)

    # ── 요약 확인 ─────────────────────────────────────────────
    print("\n" + "=" * 58)
    print("  배포 요약")
    print(f"  서버    : {host}:{ssh_port}  (유저: {username})")
    print(f"  OS      : {os_name}  ({runtime})")
    svc_names = ", ".join(SERVICES[k][0] for k in selected)
    print(f"  서비스  : {svc_names}")
    print(f"  경로    : {remote_base}")
    print("=" * 58)

    if input("\n진행하시겠습니까? (y/N): ").strip().lower() != "y":
        print("취소되었습니다.")
        sys.exit(0)

    # ── 빌드 ──────────────────────────────────────────────────
    build_dir = Path(tempfile.mkdtemp(prefix="battleship_"))
    print(f"\n[빌드 시작]")

    build_results: dict[str, tuple[bool, Path]] = {}
    for key in selected:
        svc_name, proj_name = SERVICES[key]
        out_dir = build_dir / proj_name
        out_dir.mkdir(parents=True, exist_ok=True)
        ok = build_service(proj_name, runtime, out_dir)
        build_results[key] = (ok, out_dir)

    failed = [SERVICES[k][0] for k, (ok, _) in build_results.items() if not ok]
    if failed:
        print(f"\n[오류] 빌드 실패: {', '.join(failed)}")
        shutil.rmtree(build_dir, ignore_errors=True)
        sys.exit(1)

    print("\n[빌드 완료] 모든 서비스 빌드 성공")

    # ── SSH 배포 ───────────────────────────────────────────────
    deployer = DEPLOYERS[runtime]
    try:
        ssh  = create_ssh(host, ssh_port, username, password)
        sftp = ssh.open_sftp()

        # ~ 를 실제 홈 디렉토리로 치환 (Linux/macOS)
        if "~" in remote_base:
            _, home, _ = run_remote(ssh, "echo $HOME")
            remote_base = remote_base.replace("~", home.strip())
            print(f"  배포 경로 확정: {remote_base}")

        # 의존성(Redis, MySQL) 확인 및 설치
        check_and_install_deps(ssh, runtime, password)

        for key in selected:
            svc_name, proj_name = SERVICES[key]
            _, local_dir = build_results[key]
            deployer(ssh, sftp, svc_name, proj_name, local_dir, remote_base, username, password)

        # 방화벽 설정
        configure_firewall(ssh, runtime, password)

        sftp.close()
        ssh.close()

    except paramiko.AuthenticationException:
        print("\n[오류] 인증 실패 — 유저명/비밀번호를 확인하세요.")
        sys.exit(1)
    except paramiko.SSHException as e:
        print(f"\n[오류] SSH 오류: {e}")
        sys.exit(1)
    except Exception as e:
        print(f"\n[오류] 배포 중 예외 발생: {e}")
        sys.exit(1)
    finally:
        shutil.rmtree(build_dir, ignore_errors=True)

    print("\n" + "=" * 58)
    print("  배포 완료!")
    print("=" * 58)


if __name__ == "__main__":
    main()
