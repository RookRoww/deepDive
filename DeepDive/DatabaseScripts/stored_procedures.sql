DROP PROCEDURE IF EXISTS sp_LogLaunchResult(INT, INT, VARCHAR, VARCHAR, BOOLEAN);
CREATE OR REPLACE PROCEDURE sp_LogLaunchResult(
    p_WorkspaceId INT,
    p_ActionId INT,
    p_StatusCode VARCHAR(30),
    p_StatusMessage VARCHAR(255),
    p_NeedsAttention BOOLEAN
  ) LANGUAGE plpgsql AS $$ BEGIN
INSERT INTO LaunchResults (
    WorkspaceId,
    ActionId,
    StatusCode,
    StatusMessage,
    NeedsAttention,
    LaunchedAt
  )
VALUES (
    p_WorkspaceId,
    p_ActionId,
    p_StatusCode,
    p_StatusMessage,
    p_NeedsAttention,
    CURRENT_TIMESTAMP
  );
END;
$$;
DROP FUNCTION IF EXISTS sp_get_user_workspaces(INT);
CREATE OR REPLACE FUNCTION sp_get_user_workspaces(p_UserId INT) RETURNS TABLE (
    workspace_id INT,
    workspace_name VARCHAR(100),
    color_hex VARCHAR(7),
    icon_key VARCHAR(50),
    display_order INT
  ) LANGUAGE plpgsql AS $$ BEGIN RETURN QUERY
SELECT WorkspaceId,
  WorkspaceName,
  ColorHex,
  IconKey,
  DisplayOrder
FROM Workspaces
WHERE UserId = p_UserId
ORDER BY DisplayOrder ASC;
END;
$$;
DROP PROCEDURE IF EXISTS sp_get_or_create_user(VARCHAR, VARCHAR);
CREATE OR REPLACE PROCEDURE sp_get_or_create_user(
    p_WindowsUsername VARCHAR(100),
    p_DisplayName VARCHAR(100)
  ) LANGUAGE plpgsql AS $$ BEGIN
INSERT INTO Users (
    WindowsUsername,
    DisplayName,
    LaunchAtStartup,
    IsSetupComplete
  )
VALUES (p_WindowsUsername, p_DisplayName, FALSE, FALSE) ON CONFLICT (WindowsUsername) DO NOTHING;
END;
$$;
DROP PROCEDURE IF EXISTS sp_create_workspace(INT, VARCHAR, VARCHAR, VARCHAR, INT);
CREATE OR REPLACE PROCEDURE sp_create_workspace(
    p_UserId INT,
    p_WorkspaceName VARCHAR(100),
    p_ColorHex VARCHAR(7),
    p_IconKey VARCHAR(50),
    p_DisplayOrder INT
  ) LANGUAGE plpgsql AS $$ BEGIN
INSERT INTO Workspaces (
    UserId,
    WorkspaceName,
    ColorHex,
    IconKey,
    DisplayOrder,
    CreatedAt,
    UpdatedAt
  )
VALUES (
    p_UserId,
    p_WorkspaceName,
    p_ColorHex,
    p_IconKey,
    p_DisplayOrder,
    CURRENT_TIMESTAMP,
    CURRENT_TIMESTAMP
  );
END;
$$;
DROP PROCEDURE IF EXISTS sp_seed_test_workspace(INT);
CREATE OR REPLACE PROCEDURE sp_seed_test_workspace(p_UserId INT) LANGUAGE plpgsql AS $$
DECLARE v_NewWorkspaceId INT;
v_AppTypeId INT;
v_FolderTypeId INT;
v_WebTypeId INT;
BEGIN -- Grab ActionType IDs dynamically
SELECT ActionTypeId INTO v_AppTypeId
FROM ActionTypes
WHERE TypeCode = 'APP';
SELECT ActionTypeId INTO v_FolderTypeId
FROM ActionTypes
WHERE TypeCode = 'FOLDER';
SELECT ActionTypeId INTO v_WebTypeId
FROM ActionTypes
WHERE TypeCode = 'WEBSITE';
-- Insert a sample test workspace
INSERT INTO Workspaces (
    UserId,
    WorkspaceName,
    ColorHex,
    IconKey,
    DisplayOrder,
    CreatedAt,
    UpdatedAt
  )
VALUES (
    p_UserId,
    'Automated Test Suite',
    '#3B82F6',
    'flask',
    99,
    CURRENT_TIMESTAMP,
    CURRENT_TIMESTAMP
  )
RETURNING WorkspaceId INTO v_NewWorkspaceId;
-- Insert dummy actions into the test workspace
INSERT INTO WorkspaceActions (
    WorkspaceId,
    ActionTypeId,
    ActionName,
    TargetPath,
    ActionOrder,
    IsEnabled
  )
VALUES (
    v_NewWorkspaceId,
    v_AppTypeId,
    'Visual Studio Code',
    'C:\\Program Files\\Microsoft VS Code\\Code.exe',
    1,
    TRUE
  ),
  (
    v_NewWorkspaceId,
    v_FolderTypeId,
    'Project Root',
    'C:\\DeepDive\\Workspace',
    2,
    TRUE
  ),
  (
    v_NewWorkspaceId,
    v_WebTypeId,
    'Render Dashboard',
    'https://dashboard.render.com',
    3,
    TRUE
  );
END;
$$;
DROP PROCEDURE IF EXISTS sp_stress_test_audit_log(INT, INT, INT);
CREATE OR REPLACE PROCEDURE sp_stress_test_audit_log(
    p_WorkspaceId INT,
    p_ActionId INT,
    p_Iterations INT
  ) LANGUAGE plpgsql AS $$
DECLARE i INT;
BEGIN FOR i IN 1..p_Iterations LOOP
INSERT INTO LaunchResults (
    WorkspaceId,
    ActionId,
    StatusCode,
    StatusMessage,
    NeedsAttention,
    LaunchedAt
  )
VALUES (
    p_WorkspaceId,
    p_ActionId,
    CASE
      WHEN i % 5 = 0 THEN 'WARNING'
      ELSE 'SUCCESS'
    END,
    'Stress test loop execution batch iteration #' || i,
    (i % 5 = 0),
    CURRENT_TIMESTAMP
  );
END LOOP;
END;
$$;
DROP PROCEDURE IF EXISTS sp_cleanup_stale_audit_logs(INT);
CREATE OR REPLACE PROCEDURE sp_cleanup_stale_audit_logs(p_RetentionDays INT) LANGUAGE plpgsql AS $$ BEGIN
DELETE FROM LaunchResults
WHERE LaunchedAt < CURRENT_TIMESTAMP - (p_RetentionDays || ' days')::INTERVAL;
END;
$$;
DROP FUNCTION IF EXISTS sp_get_workspace_actions(INT);
CREATE OR REPLACE FUNCTION sp_get_workspace_actions(p_WorkspaceId INT) RETURNS TABLE (
    action_id INT,
    action_type_code VARCHAR(20),
    action_name VARCHAR(100),
    target_path VARCHAR(1000),
    action_order INT
  ) LANGUAGE plpgsql AS $$ BEGIN RETURN QUERY
SELECT a.ActionId,
  t.TypeCode,
  a.ActionName,
  a.TargetPath,
  a.ActionOrder
FROM WorkspaceActions a
  JOIN ActionTypes t ON a.ActionTypeId = t.ActionTypeId
WHERE a.WorkspaceId = p_WorkspaceId
  AND a.IsEnabled = TRUE
ORDER BY a.ActionOrder ASC;
END;
$$;
DROP FUNCTION IF EXISTS sp_get_database_health_metrics();
CREATE OR REPLACE FUNCTION sp_get_database_health_metrics(
    OUT total_users INT,
    OUT total_workspaces INT,
    OUT total_actions INT,
    OUT total_launches INT,
    OUT flagged_failures INT
  ) LANGUAGE plpgsql AS $$ BEGIN
SELECT COUNT(*) INTO total_users
FROM Users;
SELECT COUNT(*) INTO total_workspaces
FROM Workspaces;
SELECT COUNT(*) INTO total_actions
FROM WorkspaceActions;
SELECT COUNT(*) INTO total_launches
FROM LaunchResults;
SELECT COUNT(*) INTO flagged_failures
FROM LaunchResults
WHERE NeedsAttention = TRUE;
END;
$$;
DROP PROCEDURE IF EXISTS sp_update_user_preferences(INT, VARCHAR, BOOLEAN, INT, BOOLEAN);
CREATE OR REPLACE PROCEDURE sp_update_user_preferences(
    p_UserId INT,
    p_DisplayName VARCHAR(100),
    p_LaunchAtStartup BOOLEAN,
    p_DefaultWorkspaceId INT,
    p_IsSetupComplete BOOLEAN
  ) LANGUAGE plpgsql AS $$ BEGIN
UPDATE Users
SET DisplayName = p_DisplayName,
  LaunchAtStartup = p_LaunchAtStartup,
  DefaultWorkspaceId = p_DefaultWorkspaceId,
  IsSetupComplete = p_IsSetupComplete
WHERE UserId = p_UserId;
END;
$$;
DROP PROCEDURE IF EXISTS sp_delete_user(INT);
CREATE OR REPLACE PROCEDURE sp_delete_user(p_UserId INT) LANGUAGE plpgsql AS $$ BEGIN
DELETE FROM Users
WHERE UserId = p_UserId;
END;
$$;
DROP PROCEDURE IF EXISTS sp_update_workspace(INT, INT, VARCHAR, VARCHAR, VARCHAR);
CREATE OR REPLACE PROCEDURE sp_update_workspace(
    p_WorkspaceId INT,
    p_UserId INT,
    p_WorkspaceName VARCHAR(100),
    p_ColorHex VARCHAR(7),
    p_IconKey VARCHAR(50)
  ) LANGUAGE plpgsql AS $$ BEGIN
UPDATE Workspaces
SET WorkspaceName = p_WorkspaceName,
  ColorHex = p_ColorHex,
  IconKey = p_IconKey,
  UpdatedAt = CURRENT_TIMESTAMP
WHERE WorkspaceId = p_WorkspaceId
  AND UserId = p_UserId;
END;
$$;
DROP PROCEDURE IF EXISTS sp_update_workspace_order(INT, INT);
CREATE OR REPLACE PROCEDURE sp_update_workspace_order(
    p_WorkspaceId INT,
    p_DisplayOrder INT
  ) LANGUAGE plpgsql AS $$ BEGIN
UPDATE Workspaces
SET DisplayOrder = p_DisplayOrder,
  UpdatedAt = CURRENT_TIMESTAMP
WHERE WorkspaceId = p_WorkspaceId;
END;
$$;
DROP PROCEDURE IF EXISTS sp_delete_workspace(INT, INT);
CREATE OR REPLACE PROCEDURE sp_delete_workspace(p_WorkspaceId INT, p_UserId INT) LANGUAGE plpgsql AS $$ BEGIN
DELETE FROM Workspaces
WHERE WorkspaceId = p_WorkspaceId
  AND UserId = p_UserId;
END;
$$;
DROP PROCEDURE IF EXISTS sp_upsert_action_type(VARCHAR, VARCHAR, VARCHAR);
CREATE OR REPLACE PROCEDURE sp_upsert_action_type(
    p_TypeCode VARCHAR(20),
    p_DisplayName VARCHAR(50),
    p_AllowedFormat VARCHAR(150)
  ) LANGUAGE plpgsql AS $$ BEGIN
INSERT INTO ActionTypes (TypeCode, DisplayName, AllowedFormat)
VALUES (p_TypeCode, p_DisplayName, p_AllowedFormat) ON CONFLICT (TypeCode) DO
UPDATE
SET DisplayName = EXCLUDED.DisplayName,
  AllowedFormat = EXCLUDED.AllowedFormat;
END;
$$;
DROP PROCEDURE IF EXISTS sp_delete_action_type(INT);
CREATE OR REPLACE PROCEDURE sp_delete_action_type(p_ActionTypeId INT) LANGUAGE plpgsql AS $$ BEGIN
DELETE FROM ActionTypes
WHERE ActionTypeId = p_ActionTypeId;
END;
$$;
DROP PROCEDURE IF EXISTS sp_add_workspace_action(INT, INT, VARCHAR, VARCHAR, INT, BOOLEAN);
CREATE OR REPLACE PROCEDURE sp_add_workspace_action(
    p_WorkspaceId INT,
    p_ActionTypeId INT,
    p_ActionName VARCHAR(100),
    p_TargetPath VARCHAR(1000),
    p_ActionOrder INT,
    p_IsEnabled BOOLEAN
  ) LANGUAGE plpgsql AS $$ BEGIN
INSERT INTO WorkspaceActions (
    WorkspaceId,
    ActionTypeId,
    ActionName,
    TargetPath,
    ActionOrder,
    IsEnabled
  )
VALUES (
    p_WorkspaceId,
    p_ActionTypeId,
    p_ActionName,
    p_TargetPath,
    p_ActionOrder,
    p_IsEnabled
  );
END;
$$;
DROP PROCEDURE IF EXISTS sp_update_workspace_action(INT, INT, INT, VARCHAR, VARCHAR, INT, BOOLEAN);
CREATE OR REPLACE PROCEDURE sp_update_workspace_action(
    p_ActionId INT,
    p_WorkspaceId INT,
    p_ActionTypeId INT,
    p_ActionName VARCHAR(100),
    p_TargetPath VARCHAR(1000),
    p_ActionOrder INT,
    p_IsEnabled BOOLEAN
  ) LANGUAGE plpgsql AS $$ BEGIN
UPDATE WorkspaceActions
SET ActionTypeId = p_ActionTypeId,
  ActionName = p_ActionName,
  TargetPath = p_TargetPath,
  ActionOrder = p_ActionOrder,
  IsEnabled = p_IsEnabled
WHERE ActionId = p_ActionId
  AND WorkspaceId = p_WorkspaceId;
END;
$$;
DROP PROCEDURE IF EXISTS sp_toggle_workspace_action(INT, BOOLEAN);
CREATE OR REPLACE PROCEDURE sp_toggle_workspace_action(p_ActionId INT, p_IsEnabled BOOLEAN) LANGUAGE plpgsql AS $$ BEGIN
UPDATE WorkspaceActions
SET IsEnabled = p_IsEnabled
WHERE ActionId = p_ActionId;
END;
$$;
DROP PROCEDURE IF EXISTS sp_delete_workspace_action(INT, INT);
CREATE OR REPLACE PROCEDURE sp_delete_workspace_action(p_ActionId INT, p_WorkspaceId INT) LANGUAGE plpgsql AS $$ BEGIN
DELETE FROM WorkspaceActions
WHERE ActionId = p_ActionId
  AND WorkspaceId = p_WorkspaceId;
END;
$$;
DROP PROCEDURE IF EXISTS sp_resolve_launch_attention(INT, VARCHAR);
CREATE OR REPLACE PROCEDURE sp_resolve_launch_attention(
    p_ResultId INT,
    p_StatusMessage VARCHAR(255)
  ) LANGUAGE plpgsql AS $$ BEGIN
UPDATE LaunchResults
SET NeedsAttention = FALSE,
  StatusMessage = p_StatusMessage
WHERE ResultId = p_ResultId;
END;
$$;
DROP FUNCTION IF EXISTS sp_get_workspace_launch_history(INT, INT);
CREATE OR REPLACE FUNCTION sp_get_workspace_launch_history(
    p_WorkspaceId INT,
    p_Limit INT DEFAULT 50
  ) RETURNS TABLE (
    result_id INT,
    action_id INT,
    action_name VARCHAR(100),
    launched_at TIMESTAMP,
    status_code VARCHAR(30),
    status_message VARCHAR(255),
    needs_attention BOOLEAN
  ) LANGUAGE plpgsql AS $$ BEGIN RETURN QUERY
SELECT lr.ResultId,
  lr.ActionId,
  wa.ActionName,
  lr.LaunchedAt,
  lr.StatusCode,
  lr.StatusMessage,
  lr.NeedsAttention
FROM LaunchResults lr
  JOIN WorkspaceActions wa ON wa.ActionId = lr.ActionId
WHERE lr.WorkspaceId = p_WorkspaceId
ORDER BY lr.LaunchedAt DESC,
  lr.ResultId DESC
LIMIT p_Limit;
END;
$$;