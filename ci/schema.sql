CREATE DATABASE IF NOT EXISTS plexus_mi2 CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE plexus_mi2;

CREATE TABLE IF NOT EXISTS study (
  id INT AUTO_INCREMENT PRIMARY KEY,
  study_uid VARCHAR(500) NOT NULL,
  patient_id INT,
  service_request_id VARCHAR(100),
  study_date DATE,
  study_time TIME,
  modality_codes VARCHAR(100),
  num_instances INT DEFAULT 0,
  upload_status ENUM('pending','success','failed') DEFAULT 'pending',
  created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  INDEX idx_study_uid (study_uid)
);

CREATE TABLE IF NOT EXISTS series (
  id INT AUTO_INCREMENT PRIMARY KEY,
  series_uid VARCHAR(500) NOT NULL,
  study_id INT,
  modality VARCHAR(20),
  series_number INT,
  num_instances INT DEFAULT 0,
  created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS instance (
  id INT AUTO_INCREMENT PRIMARY KEY,
  sop_instance_uid VARCHAR(500) NOT NULL,
  series_id INT,
  instance_number INT,
  file_path VARCHAR(500),
  upload_status ENUM('pending','success','failed') DEFAULT 'pending',
  created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS servers (
  id INT AUTO_INCREMENT PRIMARY KEY,
  ae_title VARCHAR(100) NOT NULL,
  host VARCHAR(255) NOT NULL,
  port INT NOT NULL,
  description VARCHAR(500),
  is_active BOOLEAN DEFAULT TRUE,
  created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

INSERT INTO servers (ae_title, host, port, description, is_active)
VALUES ('CAREBACKEND', 'localhost', 9000, 'CI Mock CARE Backend', TRUE);
