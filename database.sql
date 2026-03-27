-- BattleShip Database 초기화 스크립트
-- 실행: mysql -u root -p < database.sql

CREATE DATABASE IF NOT EXISTS battleship
    CHARACTER SET utf8mb4
    COLLATE utf8mb4_unicode_ci;

USE battleship;

-- ── 테이블 생성 ──────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS users (
    id            INT          NOT NULL AUTO_INCREMENT,
    username      VARCHAR(50)  NOT NULL UNIQUE,
    password_hash VARCHAR(255) NOT NULL,
    created_at    DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS game_records (
    id          INT          NOT NULL AUTO_INCREMENT,
    session_id  VARCHAR(100) NOT NULL,
    winner_id   VARCHAR(50)  NOT NULL,
    loser_id    VARCHAR(50)  NOT NULL,
    total_turns INT          NOT NULL,
    played_at   DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- ── 테스트 계정 ──────────────────────────────────────────────────────
-- 비밀번호는 평문 저장 (클라이언트에서 해시 없이 전송)

INSERT INTO users (username, password_hash) VALUES
    ('aaaa', '1111'),
    ('bbbb', '2222'),
    ('test1', 'pass1'),
    ('test2', 'pass2')
ON DUPLICATE KEY UPDATE password_hash = VALUES(password_hash);
