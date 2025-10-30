import sys
from git import Repo

current_tag = sys.argv[1]
version_kind = sys.argv[2]

print("Issuing release...")
print("Current tag : " + current_tag)
print("Version tag : " + version_kind)

version_parts = current_tag.split('.')

if version_kind == "major":
    version_parts[0] = str(int(version_parts[0]) + 1)
elif version_kind == "minor":
    version_parts[1] = str(int(version_parts[1]) + 1)
elif version_kind == "patch":
    version_parts[2]= str(int(version_parts[2]) + 1)
    
new_version = ".".join(version_parts)
print("Next version tag : " + new_version)
repo = Repo(".")
new_tag = repo.create_tag(new_version, message='Version "{0}"'.format(new_version))
repo.remotes.origin.push(new_tag)
