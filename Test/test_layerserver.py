
import grpc
import sys
import os
import json
import time

# Add current dir to path to import generated modules if checking out
sys.path.append(os.getcwd())

# We need to generate the python code from proto.
# For this script to allow easy running, we will assume the user has grpcio-tools installed 
# and we will generate the pb2 files on the fly or expect them.

# To simplify, we will use grpc.protoc to generate.
from grpc_tools import protoc

def generate_protos():
    print("Generating protos...")
    proto_path = r'UiAutomationGRPC.LayerServer/Protos/uiautomation_layer.proto'
    if not os.path.exists(proto_path):
        print(f"Proto file not found at {proto_path}")
        return False
    
    command = [
        'grpc_tools.protoc',
        '-IUiAutomationGRPC.LayerServer/Protos',
        '--python_out=.',
        '--grpc_python_out=.',
        'uiautomation_layer.proto'
    ]
    if protoc.main(command) != 0:
        print("Failed to generate protos.")
        return False
    return True

if not generate_protos():
    sys.exit(1)

import Test.uiautomation_layer_pb2
import Test.uiautomation_layer_pb2_grpc

import subprocess

def run():
    print("Launching Calculator...")
    subprocess.Popen("calc")
    time.sleep(2)

    print("Connecting to server...")
    # Port 50052 as per previous file content
    channel = grpc.insecure_channel('localhost:50052')
    stub = uiautomation_layer_pb2_grpc.UiAutomationServiceStub(channel)

    # 1. Open App / Get Structure
    # Use "Calculator" as process name which is standard for Win10 UWP Calculator in .NET Process.GetProcessesByName
    # If "Calculator" fails, we might need "CalculatorApp" or check if it's already running.
    # The user said "server is launched", implying we just run this client.
    print("1. Requesting App Structure for Calculator...")
    request = uiautomation_layer_pb2.AppStructureRequest(
        app_name="Calculator",  # Changed from CalculatorApp to Calculator to be safer with Process.GetProcessesByName
        arguments="",
        use_process_id=False
    )
    
    try:
        response = stub.GetAppStructure(request)
        if not response.success:
             # Fallback to CalculatorApp if Calculator fails
             print("Retrying with 'CalculatorApp'...")
             request.app_name = "CalculatorApp"
             response = stub.GetAppStructure(request)

        if response.success:
            print("Structure received!")
            structure = json.loads(response.json_structure)
            print(f"Root Node: {structure.get('Name')} ({structure.get('ControlType')})")
            
            # Helper to find node by Name
            def find_node(node, name):
                if node.get('Name') == name: return node
                for child in node.get('Children', []):
                    found = find_node(child, name)
                    if found: return found
                return None
            
            # Helper to perform action and update structure
            def perform_action(current_structure, element_name, action_type=uiautomation_layer_pb2.CLICK):
                node = find_node(current_structure, element_name)
                if not node:
                    print(f"Error: Could not find element '{element_name}'")
                    return None
                
                # Use UniqId (or RuntimeId if available)
                uniq_id = node.get('RuntimeId') or node.get('UniqId')
                print(f"Performing {action_type} on '{element_name}' (ID: {uniq_id})...")
                
                action_request = uiautomation_layer_pb2.PerformActionRequest(
                    runtime_id=uniq_id,
                    action=action_type,
                    arguments=[]
                )
                
                # Use PerformActionWithStructure to get updated state
                action_response = stub.PerformActionWithStructure(action_request)
                if action_response.success:
                    print(f"Action success.")
                    return json.loads(action_response.json_structure)
                else:
                    print(f"Action failed: {action_response.message}")
                    return None

            # Sequence: 9 * 9 = 
            # Button names in standard Win10 Calc: "Nine", "Multiply by", "Equals"
            # Or sometimes local dependant. Assuming English.
            
            # 1. Click "Nine"
            perform_action(structure, "Nine", uiautomation_layer_pb2.MoveTo)
            structure = perform_action(structure, "Nine",uiautomation_layer_pb2.LeftClick)
            if not structure: return

            # 2. Click "Multiply by"
            structure = perform_action(structure, "Multiply by")
            if not structure: return

            # 3. Click "Nine"
            structure = perform_action(structure, "Nine")
            if not structure: return

            # 4. Click "Equals"
            structure = perform_action(structure, "Equals")
            if not structure: return

            # 5. Get Result
            # The result is usually in a text element named "CalculatorResults" or similar.
            # It usually says "Display is 81".
            res_node = find_node(structure, "CalculatorResults")
            if not res_node:
                 # Try finding via AutomationId "CalculatorResults" if possible, but our find_node is by Name.
                 # Let's search for any node with Name starting with "Display is"
                 def find_result_node(node):
                     if node.get('Name') and str(node.get('Name')).startswith("Display is"):
                         return node
                     for child in node.get('Children', []):
                         found = find_result_node(child)
                         if found: return found
                     return None
                 res_node = find_result_node(structure)
            
            if res_node:
                print(f"Result Element Found: {res_node.get('Name')}")
            else:
                 print("Could not find result element. Dumping structure snippet...")
                 print(json.dumps(structure, indent=2)[:500])

        else:
            print(f"Failed to get structure: {response.message}")

    except grpc.RpcError as e:
        print(f"RPC Error: {e}")
    finally:
        print("Closing App...")
        try:
             close_request = uiautomation_layer_pb2.AppRequest(app_name="Calculator", arguments="")
             close_response = stub.CloseApp(close_request)
             print(f"CloseApp Response: {close_response.message}")
        except Exception as e:
             print(f"Failed to close app: {e}")

if __name__ == '__main__':
    run()
