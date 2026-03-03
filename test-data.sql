-- ====================================
-- EdgeGateway 测试数据脚本
-- 用于生成丰富的测试数据，包括设备、数据点、虚拟数据点、发送通道等
-- ====================================

-- ====================================
-- 1. 清理现有数据（可选，谨慎使用）
-- ====================================
-- DELETE FROM ChannelDataPointMappings;
-- DELETE FROM VirtualDataPoints;
-- DELETE FROM DataPoints;
-- DELETE FROM Channels;
-- DELETE FROM Devices;

-- ====================================
-- 2. 插入测试设备
-- ====================================
INSERT INTO "Devices" ("Name", "Code", "Description", "Protocol", "Address", "Port", "PollingIntervalMs", "IsEnabled", "CreatedAt")
VALUES 
-- 模拟设备 1
('测试设备 001', 'DEV_TEST_001', '温度压力测试设备', 1, '127.0.0.1', 502, 1000, TRUE, NOW()),
-- 模拟设备 2
('测试设备 002', 'DEV_TEST_002', '液位流量测试设备', 1, '127.0.0.1', 502, 1000, TRUE, NOW()),
-- 模拟设备 3
('测试设备 003', 'DEV_TEST_003', '多参数环境监测设备', 1, '127.0.0.1', 502, 1000, TRUE, NOW()),
-- 产线设备 1
('产线设备 A', 'PROD_LINE_A', 'A 产线主设备', 1, '192.168.1.101', 502, 2000, TRUE, NOW()),
-- 产线设备 2
('产线设备 B', 'PROD_LINE_B', 'B 产线主设备', 1, '192.168.1.102', 502, 2000, TRUE, NOW()),
-- 仓储设备
('仓储监测设备', 'WAREHOUSE_MON', '仓储环境监测', 1, '192.168.1.150', 502, 5000, TRUE, NOW()),
-- 动力设备
('动力站设备', 'POWER_STATION', '动力站监测', 1, '192.168.1.200', 502, 3000, TRUE, NOW()),
-- 实验室设备
('实验室设备', 'LAB_DEVICE', '实验室测试设备', 1, '192.168.2.10', 502, 1000, TRUE, NOW());

-- ====================================
-- 3. 插入测试数据点
-- ====================================
-- 设备 1 的数据点（温度、压力）
INSERT INTO "DataPoints" ("DeviceId", "Name", "Tag", "Description", "Address", "DataType", "Unit", "IsEnabled", "CreatedAt")
SELECT 
    d."Id",
    vals.name,
    vals.tag,
    vals.desc,
    vals.addr,
    vals.dtype,
    vals.unit,
    TRUE,
    NOW()
FROM "Devices" d,
LATERAL (VALUES
    ('温度 01', 'DEV_TEST_001.Temperature01', '主温度传感器', '40001', 2, '℃'),
    ('温度 02', 'DEV_TEST_001.Temperature02', '辅助温度传感器', '40003', 2, '℃'),
    ('温度 03', 'DEV_TEST_001.Temperature03', '环境温度', '40005', 2, '℃'),
    ('压力 01', 'DEV_TEST_001.Pressure01', '主压力传感器', '40007', 2, 'MPa'),
    ('压力 02', 'DEV_TEST_001.Pressure02', '辅助压力', '40009', 2, 'MPa'),
    ('湿度', 'DEV_TEST_001.Humidity', '环境湿度', '40011', 2, '%RH')
) AS vals(name, tag, desc, addr, dtype, unit)
WHERE d."Code" = 'DEV_TEST_001';

-- 设备 2 的数据点（液位、流量）
INSERT INTO "DataPoints" ("DeviceId", "Name", "Tag", "Description", "Address", "DataType", "Unit", "IsEnabled", "CreatedAt")
SELECT 
    d."Id",
    vals.name,
    vals.tag,
    vals.desc,
    vals.addr,
    vals.dtype,
    vals.unit,
    TRUE,
    NOW()
FROM "Devices" d,
LATERAL (VALUES
    ('液位 01', 'DEV_TEST_002.Level01', '主液位传感器', '40001', 2, 'm'),
    ('液位 02', 'DEV_TEST_002.Level02', '备用液位', '40003', 2, 'm'),
    ('流量 01', 'DEV_TEST_002.Flow01', '入口流量', '40005', 2, 'm³/h'),
    ('流量 02', 'DEV_TEST_002.Flow02', '出口流量', '40007', 2, 'm³/h'),
    ('温度', 'DEV_TEST_002.Temperature', '介质温度', '40009', 2, '℃'),
    ('密度', 'DEV_TEST_002.Density', '介质密度', '40011', 2, 'kg/m³')
) AS vals(name, tag, desc, addr, dtype, unit)
WHERE d."Code" = 'DEV_TEST_002';

-- 设备 3 的数据点（多参数环境）
INSERT INTO "DataPoints" ("DeviceId", "Name", "Tag", "Description", "Address", "DataType", "Unit", "IsEnabled", "CreatedAt")
SELECT 
    d."Id",
    vals.name,
    vals.tag,
    vals.desc,
    vals.addr,
    vals.dtype,
    vals.unit,
    TRUE,
    NOW()
FROM "Devices" d,
LATERAL (VALUES
    ('PM2.5', 'DEV_TEST_003.PM25', 'PM2.5 浓度', '40001', 2, 'μg/m³'),
    ('PM10', 'DEV_TEST_003.PM10', 'PM10 浓度', '40003', 2, 'μg/m³'),
    ('CO2', 'DEV_TEST_003.CO2', '二氧化碳浓度', '40005', 2, 'ppm'),
    ('TVOC', 'DEV_TEST_003.TVOC', '总挥发性有机物', '40007', 2, 'ppb'),
    ('温度', 'DEV_TEST_003.Temperature', '环境温度', '40009', 2, '℃'),
    ('湿度', 'DEV_TEST_003.Humidity', '环境湿度', '40011', 2, '%RH'),
    ('大气压', 'DEV_TEST_003.Pressure', '大气压力', '40013', 2, 'hPa'),
    ('噪声', 'DEV_TEST_003.Noise', '噪声分贝', '40015', 2, 'dB')
) AS vals(name, tag, desc, addr, dtype, unit)
WHERE d."Code" = 'DEV_TEST_003';

-- 产线设备 A 的数据点
INSERT INTO "DataPoints" ("DeviceId", "Name", "Tag", "Description", "Address", "DataType", "Unit", "IsEnabled", "CreatedAt")
SELECT 
    d."Id",
    vals.name,
    vals.tag,
    vals.desc,
    vals.addr,
    vals.dtype,
    vals.unit,
    TRUE,
    NOW()
FROM "Devices" d,
LATERAL (VALUES
    ('主轴温度', 'PROD_LINE_A.SpindleTemp', '主轴温度', '40001', 2, '℃'),
    ('主轴转速', 'PROD_LINE_A.SpindleSpeed', '主轴转速', '40003', 2, 'rpm'),
    ('进给速度', 'PROD_LINE_A.FeedRate', '进给速度', '40005', 2, 'mm/min'),
    ('切削力', 'PROD_LINE_A.CuttingForce', '切削力', '40007', 2, 'N'),
    ('振动', 'PROD_LINE_A.Vibration', '振动值', '40009', 2, 'mm/s'),
    ('功率', 'PROD_LINE_A.Power', '主轴功率', '40011', 2, 'kW')
) AS vals(name, tag, desc, addr, dtype, unit)
WHERE d."Code" = 'PROD_LINE_A';

-- 产线设备 B 的数据点
INSERT INTO "DataPoints" ("DeviceId", "Name", "Tag", "Description", "Address", "DataType", "Unit", "IsEnabled", "CreatedAt")
SELECT 
    d."Id",
    vals.name,
    vals.tag,
    vals.desc,
    vals.addr,
    vals.dtype,
    vals.unit,
    TRUE,
    NOW()
FROM "Devices" d,
LATERAL (VALUES
    ('温度', 'PROD_LINE_B.Temperature', '工作温度', '40001', 2, '℃'),
    ('压力', 'PROD_LINE_B.Pressure', '液压压力', '40003', 2, 'bar'),
    ('速度', 'PROD_LINE_B.Speed', '运行速度', '40005', 2, 'm/min'),
    ('计数', 'PROD_LINE_B.Counter', '产品计数', '40007', 1, 'pcs'),
    ('良率', 'PROD_LINE_B.YieldRate', '良率', '40009', 2, '%'),
    ('能耗', 'PROD_LINE_B.Energy', '能耗', '40011', 2, 'kWh')
) AS vals(name, tag, desc, addr, dtype, unit)
WHERE d."Code" = 'PROD_LINE_B';

-- 仓储设备数据点
INSERT INTO "DataPoints" ("DeviceId", "Name", "Tag", "Description", "Address", "DataType", "Unit", "IsEnabled", "CreatedAt")
SELECT 
    d."Id",
    vals.name,
    vals.tag,
    vals.desc,
    vals.addr,
    vals.dtype,
    vals.unit,
    TRUE,
    NOW()
FROM "Devices" d,
LATERAL (VALUES
    ('温度', 'WAREHOUSE_MON.Temperature', '仓库温度', '40001', 2, '℃'),
    ('湿度', 'WAREHOUSE_MON.Humidity', '仓库湿度', '40003', 2, '%RH'),
    ('光照', 'WAREHOUSE_MON.Light', '光照强度', '40005', 2, 'lux'),
    ('烟雾', 'WAREHOUSE_MON.Smoke', '烟雾浓度', '40007', 2, '%LEL'),
    ('水浸', 'WAREHOUSE_MON.WaterLeak', '水浸检测', '40009', 3, 'bool')
) AS vals(name, tag, desc, addr, dtype, unit)
WHERE d."Code" = 'WAREHOUSE_MON';

-- 动力站设备数据点
INSERT INTO "DataPoints" ("DeviceId", "Name", "Tag", "Description", "Address", "DataType", "Unit", "IsEnabled", "CreatedAt")
SELECT 
    d."Id",
    vals.name,
    vals.tag,
    vals.desc,
    vals.addr,
    vals.dtype,
    vals.unit,
    TRUE,
    NOW()
FROM "Devices" d,
LATERAL (VALUES
    ('电压 A', 'POWER_STATION.VoltageA', 'A 相电压', '40001', 2, 'V'),
    ('电压 B', 'POWER_STATION.VoltageB', 'B 相电压', '40003', 2, 'V'),
    ('电压 C', 'POWER_STATION.VoltageC', 'C 相电压', '40005', 2, 'V'),
    ('电流 A', 'POWER_STATION.CurrentA', 'A 相电流', '40007', 2, 'A'),
    ('电流 B', 'POWER_STATION.CurrentB', 'B 相电流', '40009', 2, 'A'),
    ('电流 C', 'POWER_STATION.CurrentC', 'C 相电流', '40011', 2, 'A'),
    ('功率因数', 'POWER_STATION.PowerFactor', '功率因数', '40013', 2, ''),
    ('频率', 'POWER_STATION.Frequency', '电网频率', '40015', 2, 'Hz'),
    ('累计电量', 'POWER_STATION.TotalEnergy', '累计电量', '40017', 2, 'kWh')
) AS vals(name, tag, desc, addr, dtype, unit)
WHERE d."Code" = 'POWER_STATION';

-- 实验室设备数据点
INSERT INTO "DataPoints" ("DeviceId", "Name", "Tag", "Description", "Address", "DataType", "Unit", "IsEnabled", "CreatedAt")
SELECT 
    d."Id",
    vals.name,
    vals.tag,
    vals.desc,
    vals.addr,
    vals.dtype,
    vals.unit,
    TRUE,
    NOW()
FROM "Devices" d,
LATERAL (VALUES
    ('温度', 'LAB_DEVICE.Temperature', '实验室温度', '40001', 2, '℃'),
    ('湿度', 'LAB_DEVICE.Humidity', '实验室湿度', '40003', 2, '%RH'),
    ('洁净度', 'LAB_DEVICE.Cleanliness', '空气洁净度', '40005', 2, '级'),
    ('压差', 'LAB_DEVICE.PressureDiff', '房间压差', '40007', 2, 'Pa'),
    ('风速', 'LAB_DEVICE.AirSpeed', '送风风速', '40009', 2, 'm/s')
) AS vals(name, tag, desc, addr, dtype, unit)
WHERE d."Code" = 'LAB_DEVICE';

-- ====================================
-- 4. 插入测试虚拟数据点
-- ====================================
INSERT INTO "VirtualDataPoints" ("DeviceId", "Name", "Tag", "Description", "Expression", "CalculationType", "DataType", "Unit", "IsEnabled", "DependencyTags", "CreatedAt")
SELECT 
    d."Id",
    vals.name,
    vals.tag,
    vals.desc,
    vals.expr,
    vals.calctype,
    vals.dtype,
    vals.unit,
    TRUE,
    vals.deps,
    NOW()
FROM "Devices" d,
LATERAL (VALUES
    -- 设备 1 的虚拟点
    ('平均温度', 'DEV_TEST_001.Virtual.AvgTemp', '温度平均值', 'Avg(DEV_TEST_001.Temperature01, DEV_TEST_001.Temperature02, DEV_TEST_001.Temperature03)', 2, 2, '℃', '["DEV_TEST_001.Temperature01","DEV_TEST_001.Temperature02","DEV_TEST_001.Temperature03"]'),
    ('温压积', 'DEV_TEST_001.Virtual.TempPressProd', '温度压力乘积', 'DEV_TEST_001.Temperature01 * DEV_TEST_001.Pressure01', 3, 2, '', '["DEV_TEST_001.Temperature01","DEV_TEST_001.Pressure01"]'),
    ('温差', 'DEV_TEST_001.Virtual.TempDiff', '温度差值', 'DEV_TEST_001.Temperature01 - DEV_TEST_001.Temperature02', 4, 2, '℃', '["DEV_TEST_001.Temperature01","DEV_TEST_001.Temperature02"]'),
    -- 设备 2 的虚拟点
    ('流量差', 'DEV_TEST_002.Virtual.FlowDiff', '进出口流量差', 'DEV_TEST_002.Flow01 - DEV_TEST_002.Flow02', 4, 2, 'm³/h', '["DEV_TEST_002.Flow01","DEV_TEST_002.Flow02"]'),
    ('平均液位', 'DEV_TEST_002.Virtual.AvgLevel', '平均液位', 'Avg(DEV_TEST_002.Level01, DEV_TEST_002.Level02)', 2, 2, 'm', '["DEV_TEST_002.Level01","DEV_TEST_002.Level02"]'),
    -- 设备 3 的虚拟点
    ('空气质量指数', 'DEV_TEST_003.Virtual.AQI', '简易空气质量指数', '(DEV_TEST_003.PM25 + DEV_TEST_003.PM10) / 2', 2, 2, 'μg/m³', '["DEV_TEST_003.PM25","DEV_TEST_003.PM10"]'),
    ('舒适度指数', 'DEV_TEST_003.Virtual.ComfortIndex', '温湿度舒适度', 'DEV_TEST_003.Temperature * 0.6 + DEV_TEST_003.Humidity * 0.4', 3, 2, '', '["DEV_TEST_003.Temperature","DEV_TEST_003.Humidity"]'),
    -- 产线 A 的虚拟点
    ('平均温度', 'PROD_LINE_A.Virtual.AvgTemp', '平均工作温度', 'Avg(PROD_LINE_A.SpindleTemp, PROD_LINE_A.Temperature)', 2, 2, '℃', '["PROD_LINE_A.SpindleTemp"]'),
    ('能效比', 'PROD_LINE_A.Virtual.Efficiency', '能效比', 'PROD_LINE_A.Power / NULLIF(PROD_LINE_A.SpindleSpeed, 0)', 5, 2, 'kW/rpm', '["PROD_LINE_A.Power","PROD_LINE_A.SpindleSpeed"]'),
    -- 产线 B 的虚拟点
    ('总能耗', 'PROD_LINE_B.Virtual.TotalEnergy', '累计总能耗', 'PROD_LINE_B.Energy * 24', 3, 2, 'kWh', '["PROD_LINE_B.Energy"]'),
    -- 动力站虚拟点
    ('三相平均电压', 'POWER_STATION.Virtual.AvgVoltage', '三相平均电压', '(POWER_STATION.VoltageA + POWER_STATION.VoltageB + POWER_STATION.VoltageC) / 3', 2, 2, 'V', '["POWER_STATION.VoltageA","POWER_STATION.VoltageB","POWER_STATION.VoltageC"]'),
    ('三相平均电流', 'POWER_STATION.Virtual.AvgCurrent', '三相平均电流', '(POWER_STATION.CurrentA + POWER_STATION.CurrentB + POWER_STATION.CurrentC) / 3', 2, 2, 'A', '["POWER_STATION.CurrentA","POWER_STATION.CurrentB","POWER_STATION.CurrentC"]'),
    ('总功率', 'POWER_STATION.Virtual.TotalPower', '估算总功率', 'POWER_STATION.VoltageA * POWER_STATION.CurrentA + POWER_STATION.VoltageB * POWER_STATION.CurrentB + POWER_STATION.VoltageC * POWER_STATION.CurrentC', 3, 2, 'kW', '["POWER_STATION.VoltageA","POWER_STATION.CurrentA","POWER_STATION.VoltageB","POWER_STATION.CurrentB","POWER_STATION.VoltageC","POWER_STATION.CurrentC"]')
) AS vals(name, tag, desc, expr, calctype, dtype, unit, deps)
WHERE d."Code" IN ('DEV_TEST_001', 'DEV_TEST_002', 'DEV_TEST_003', 'PROD_LINE_A', 'PROD_LINE_B', 'POWER_STATION');

-- ====================================
-- 5. 插入测试发送通道
-- ====================================
INSERT INTO "Channels" ("Name", "Code", "Description", "Protocol", "Endpoint", "IsEnabled", "CreatedAt", 
    "MqttTopic", "MqttClientId", "MqttUsername", "MqttPassword", "MqttQos",
    "HttpMethod", "HttpToken", "HttpTimeout", "HttpMode",
    "WsSubscribeTopic", "WsHeartbeatInterval",
    "FileFormat", "FilePath")
VALUES
-- MQTT 通道
('MQTT 数据上报', 'MQTT_UPLOAD', '上报到 MQTT Broker', 0, 'mqtt://127.0.0.1:1883', TRUE, NOW(), 
    'edge/data', 'edge_gateway_001', NULL, NULL, 0,
    NULL, NULL, NULL, NULL,
    NULL, NULL,
    NULL, NULL),

-- HTTP 客户端通道
('HTTP API 推送', 'HTTP_PUSH', '推送到 HTTP API', 1, 'http://127.0.0.1:8080/api/data', TRUE, NOW(),
    NULL, NULL, NULL, NULL, NULL,
    'POST', 'Bearer test_token_123', 5000, 'client',
    NULL, NULL,
    NULL, NULL),

-- HTTP 服务端通道
('HTTP 数据接口', 'HTTP_SERVER', '提供 HTTP 数据接口', 1, '/api/v1/telemetry', TRUE, NOW(),
    NULL, NULL, NULL, NULL, NULL,
    'POST', NULL, 5000, 'server',
    NULL, NULL,
    NULL, NULL),

-- WebSocket 通道
('WebSocket 推送', 'WS_PUSH', 'WebSocket 实时推送', 2, '/ws', TRUE, NOW(),
    NULL, NULL, NULL, NULL, NULL,
    NULL, NULL, NULL, NULL,
    'edge/telemetry', 30000,
    NULL, NULL),

-- 本地文件通道
('本地文件记录', 'FILE_LOG', '本地文件记录', 3, './output/gateway_data.json', TRUE, NOW(),
    NULL, NULL, NULL, NULL, NULL,
    NULL, NULL, NULL, NULL,
    NULL, NULL,
    'json', './output/gateway_data.json'),

-- 测试通道（禁用）
('测试通道', 'TEST_CHANNEL', '测试用通道', 0, 'mqtt://test:1883', FALSE, NOW(),
    'test/data', 'test_client', NULL, NULL, 0,
    NULL, NULL, NULL, NULL,
    NULL, NULL,
    NULL, NULL);

-- ====================================
-- 6. 插入通道 - 数据点映射关系
-- ====================================
-- MQTT 通道映射 - 设备 1 的所有数据点
INSERT INTO "ChannelDataPointMappings" ("ChannelId", "DataPointId", "IsEnabled", "CreatedAt")
SELECT c."Id", dp."Id", TRUE, NOW()
FROM "Channels" c, "DataPoints" dp
WHERE c."Code" = 'MQTT_UPLOAD' 
AND dp."Tag" LIKE 'DEV_TEST_001.%';

-- MQTT 通道映射 - 设备 1 的虚拟数据点
INSERT INTO "ChannelDataPointMappings" ("ChannelId", "VirtualDataPointId", "IsEnabled", "CreatedAt")
SELECT c."Id", vp."Id", TRUE, NOW()
FROM "Channels" c, "VirtualDataPoints" vp
WHERE c."Code" = 'MQTT_UPLOAD' 
AND vp."Tag" LIKE 'DEV_TEST_001.Virtual.%';

-- HTTP 推送通道映射 - 设备 3 的所有数据点
INSERT INTO "ChannelDataPointMappings" ("ChannelId", "DataPointId", "IsEnabled", "CreatedAt")
SELECT c."Id", dp."Id", TRUE, NOW()
FROM "Channels" c, "DataPoints" dp
WHERE c."Code" = 'HTTP_PUSH' 
AND dp."Tag" LIKE 'DEV_TEST_003.%';

-- HTTP 推送通道映射 - 设备 3 的虚拟数据点
INSERT INTO "ChannelDataPointMappings" ("ChannelId", "VirtualDataPointId", "IsEnabled", "CreatedAt")
SELECT c."Id", vp."Id", TRUE, NOW()
FROM "Channels" c, "VirtualDataPoints" vp
WHERE c."Code" = 'HTTP_PUSH' 
AND vp."Tag" LIKE 'DEV_TEST_003.Virtual.%';

-- HTTP 服务端通道映射 - 产线设备 A
INSERT INTO "ChannelDataPointMappings" ("ChannelId", "DataPointId", "IsEnabled", "CreatedAt")
SELECT c."Id", dp."Id", TRUE, NOW()
FROM "Channels" c, "DataPoints" dp
WHERE c."Code" = 'HTTP_SERVER' 
AND dp."Tag" LIKE 'PROD_LINE_A.%';

-- WebSocket 通道映射 - 动力站设备
INSERT INTO "ChannelDataPointMappings" ("ChannelId", "DataPointId", "IsEnabled", "CreatedAt")
SELECT c."Id", dp."Id", TRUE, NOW()
FROM "Channels" c, "DataPoints" dp
WHERE c."Code" = 'WS_PUSH' 
AND dp."Tag" LIKE 'POWER_STATION.%';

-- WebSocket 通道映射 - 动力站虚拟数据点
INSERT INTO "ChannelDataPointMappings" ("ChannelId", "VirtualDataPointId", "IsEnabled", "CreatedAt")
SELECT c."Id", vp."Id", TRUE, NOW()
FROM "Channels" c, "VirtualDataPoints" vp
WHERE c."Code" = 'WS_PUSH' 
AND vp."Tag" LIKE 'POWER_STATION.Virtual.%';

-- 本地文件通道映射 - 所有数据点
INSERT INTO "ChannelDataPointMappings" ("ChannelId", "DataPointId", "IsEnabled", "CreatedAt")
SELECT c."Id", dp."Id", TRUE, NOW()
FROM "Channels" c, "DataPoints" dp
WHERE c."Code" = 'FILE_LOG';

-- 本地文件通道映射 - 所有虚拟数据点
INSERT INTO "ChannelDataPointMappings" ("ChannelId", "VirtualDataPointId", "IsEnabled", "CreatedAt")
SELECT c."Id", vp."Id", TRUE, NOW()
FROM "Channels" c, "VirtualDataPoints" vp
WHERE c."Code" = 'FILE_LOG';

-- ====================================
-- 7. 数据验证查询
-- ====================================
-- 查看统计数据
SELECT 'Devices' AS "Table", COUNT(*) AS "Count" FROM "Devices"
UNION ALL
SELECT 'DataPoints', COUNT(*) FROM "DataPoints"
UNION ALL
SELECT 'VirtualDataPoints', COUNT(*) FROM "VirtualDataPoints"
UNION ALL
SELECT 'Channels', COUNT(*) FROM "Channels"
UNION ALL
SELECT 'ChannelDataPointMappings', COUNT(*) FROM "ChannelDataPointMappings";

-- 查看设备列表
SELECT "Id", "Name", "Code", "Protocol", "Address", "IsEnabled" 
FROM "Devices" 
ORDER BY "Id";

-- 查看虚拟数据点
SELECT "Id", "DeviceId", "Name", "Tag", "Expression", "DataType", "Unit"
FROM "VirtualDataPoints"
ORDER BY "Tag";

-- 查看通道及映射数量
SELECT 
    c."Id",
    c."Name",
    c."Code",
    c."Protocol",
    c."Endpoint",
    c."IsEnabled",
    (SELECT COUNT(*) FROM "ChannelDataPointMappings" m WHERE m."ChannelId" = c."Id" AND m."DataPointId" IS NOT NULL) AS "DataPointCount",
    (SELECT COUNT(*) FROM "ChannelDataPointMappings" m WHERE m."ChannelId" = c."Id" AND m."VirtualDataPointId" IS NOT NULL) AS "VirtualDataPointCount"
FROM "Channels" c
ORDER BY c."Id";
